using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Cisharpai.Models;
using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Embeddings;

/// <summary>
/// Embeds chunks in bounded batches with optional concurrency, retry, and token-aware splitting.
/// Failed batches are reported but do not stop the run; cancellation and transport exceptions propagate.
/// </summary>
public sealed class BulkEmbeddingProcessor : IBulkEmbeddingProcessor
{
    private readonly IEmbeddingClient _client;
    private readonly BulkEmbeddingOptions _options;

    public BulkEmbeddingProcessor(IEmbeddingClient client, BulkEmbeddingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = (options ?? new BulkEmbeddingOptions()).Snapshot();
    }

    public IAsyncEnumerable<EmbeddingBatchResult> EmbedAsync(
        IEnumerable<TextChunk> chunks,
        IProgress<BulkEmbeddingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return EmbedCoreAsync(AsAsync(chunks, cancellationToken), progress, cancellationToken);
    }

    public IAsyncEnumerable<EmbeddingBatchResult> EmbedAsync(
        IAsyncEnumerable<TextChunk> chunks,
        IProgress<BulkEmbeddingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return EmbedCoreAsync(chunks, progress, cancellationToken);
    }

    private async IAsyncEnumerable<EmbeddingBatchResult> EmbedCoreAsync(
        IAsyncEnumerable<TextChunk> chunks,
        IProgress<BulkEmbeddingProgress>? progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_options.MaxConcurrency <= 1)
        {
            await foreach (var result in EmbedSequentialAsync(chunks, progress, cancellationToken))
                yield return result;
        }
        else
        {
            await foreach (var result in EmbedConcurrentAsync(chunks, progress, cancellationToken))
                yield return result;
        }
    }

    private async IAsyncEnumerable<EmbeddingBatchResult> EmbedSequentialAsync(
        IAsyncEnumerable<TextChunk> chunks,
        IProgress<BulkEmbeddingProgress>? progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var enumerator = chunks.GetAsyncEnumerator(cancellationToken);
        long batchIndex = 0;
        int? expectedDimensions = _options.Dimensions;
        long totalChunksProcessed = 0;
        long failedBatches = 0;
        TextChunk? carryover = null;

        while (true)
        {
            List<TextChunk> batch;
            (batch, carryover) = await CollectBatchAsync(enumerator, carryover, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (batch.Count == 0) yield break;

            var result = await ProcessBatchWithRetryAsync(batch, batchIndex, expectedDimensions, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (result.IsSuccess && expectedDimensions is null && result.Response.Embeddings.Count > 0)
                expectedDimensions = result.Response.Embeddings[0].Length;

            batchIndex++;
            totalChunksProcessed += batch.Count;
            if (!result.IsSuccess) failedBatches++;

            progress?.Report(new BulkEmbeddingProgress(batchIndex, totalChunksProcessed, failedBatches));
            yield return result;
        }
    }

    private async IAsyncEnumerable<EmbeddingBatchResult> EmbedConcurrentAsync(
        IAsyncEnumerable<TextChunk> chunks,
        IProgress<BulkEmbeddingProgress>? progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var channel = Channel.CreateUnbounded<EmbeddingBatchResult>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        var dimensionState = new DimensionState(_options.Dimensions);

        var producerTask = ProduceAndProcessAsync(chunks, channel.Writer, dimensionState, linkedCts.Token);

        long completedBatches = 0;
        long totalChunksProcessed = 0;
        long failedBatches = 0;
        var buffer = new SortedDictionary<long, EmbeddingBatchResult>();
        long nextYieldIndex = 0;
        var consumerDone = false;

        try
        {
            await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
            {
                buffer[result.BatchIndex] = result;
                while (buffer.Remove(nextYieldIndex, out var next))
                {
                    nextYieldIndex++;
                    completedBatches++;
                    totalChunksProcessed += next.Chunks.Count;
                    if (!next.IsSuccess) failedBatches++;
                    progress?.Report(new BulkEmbeddingProgress(completedBatches, totalChunksProcessed, failedBatches));
                    yield return next;
                }
            }
            consumerDone = true;
        }
        finally
        {
            if (!consumerDone)
                await linkedCts.CancelAsync().ConfigureAwait(false);
            try { await producerTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                // Expected when the consumer breaks out early — cancellation is the normal shutdown path
            }
        }
    }

    private async Task ProduceAndProcessAsync(
        IAsyncEnumerable<TextChunk> chunks,
        ChannelWriter<EmbeddingBatchResult> writer,
        DimensionState dimensionState,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(_options.MaxConcurrency);
        var tasks = new List<Task>();
        Exception? producerException = null;

        try
        {
            await using var enumerator = chunks.GetAsyncEnumerator(cancellationToken);
            long batchIndex = 0;
            TextChunk? carryover = null;

            while (true)
            {
                List<TextChunk> batch;
                (batch, carryover) = await CollectBatchAsync(enumerator, carryover, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (batch.Count == 0) break;

                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                var idx = batchIndex++;
                tasks.Add(ProcessAndWriteAsync(batch, idx, writer, semaphore, dimensionState, cancellationToken));
            }
        }
        catch (Exception ex)
        {
            producerException = ex;
        }
        finally
        {
            try
            {
                if (tasks.Count > 0)
                    await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                producerException ??= ex;
            }
            writer.TryComplete(producerException);
        }
    }

    private async Task ProcessAndWriteAsync(
        List<TextChunk> batch, long batchIndex,
        ChannelWriter<EmbeddingBatchResult> writer,
        SemaphoreSlim semaphore,
        DimensionState dimensionState,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessBatchWithRetryAsync(
                batch, batchIndex, dimensionState.ExpectedDimensions, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess && result.Response.Embeddings.Count > 0)
                dimensionState.TrySetInferred(result.Response.Embeddings[0].Length);

            await writer.WriteAsync(result, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<EmbeddingBatchResult> ProcessBatchWithRetryAsync(
        List<TextChunk> batch, long batchIndex, int? expectedDimensions,
        CancellationToken cancellationToken)
    {
        var request = new EmbeddingRequest(
            batch.Select(chunk => chunk.Text).ToArray(),
            Model: _options.Model,
            InputType: _options.InputType,
            Dimensions: _options.Dimensions,
            EncodingFormat: "float",
            IncludeRawResponse: _options.IncludeRawResponse,
            ExtraParameters: _options.ExtraParameters);
        var isTransient = _options.IsTransientError ?? DefaultIsTransient;

        var attempt = 0;
        while (true)
        {
            var response = await _client.GetEmbeddingsAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (response.IsSuccess)
            {
                var error = Validate(response, batch.Count, expectedDimensions);
                if (error is not null)
                    response = response with { IsSuccess = false, ErrorMessage = error };
                else
                    expectedDimensions ??= response.Embeddings[0].Length;
            }

            if (response.IsSuccess)
            {
                var items = batch.Select((chunk, i) => new ChunkEmbedding(chunk, response.Embeddings[i])).ToArray();
                return new EmbeddingBatchResult(batchIndex, batch.AsReadOnly(), items, response);
            }

            if (attempt < _options.MaxRetries && isTransient(response))
            {
                var delay = ComputeRetryDelay(attempt);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                attempt++;
                continue;
            }

            return new EmbeddingBatchResult(batchIndex, batch.AsReadOnly(), Array.Empty<ChunkEmbedding>(), response);
        }
    }

    private TimeSpan ComputeRetryDelay(int attempt)
    {
        var baseMs = _options.RetryBaseDelay.TotalMilliseconds;
        var exponential = baseMs * Math.Pow(2, attempt);
        var jitter = Random.Shared.NextDouble() * exponential * 0.5;
        return TimeSpan.FromMilliseconds(exponential + jitter);
    }

    public static bool DefaultIsTransient(EmbeddingResponse response)
    {
        if (response.IsSuccess) return false;
        var msg = response.ErrorMessage;
        if (string.IsNullOrEmpty(msg)) return false;
        return msg.Contains("429", StringComparison.Ordinal) ||
               msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("throttl", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("500", StringComparison.Ordinal) ||
               msg.Contains("502", StringComparison.Ordinal) ||
               msg.Contains("503", StringComparison.Ordinal) ||
               msg.Contains("504", StringComparison.Ordinal) ||
               msg.Contains("internal server error", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("service unavailable", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("bad gateway", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("gateway timeout", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(List<TextChunk> Batch, TextChunk? Carryover)> CollectBatchAsync(
        IAsyncEnumerator<TextChunk> enumerator, TextChunk? carryover, CancellationToken cancellationToken)
    {
        var batch = new List<TextChunk>();
        long runningTokens = 0;

        if (carryover is not null)
        {
            batch.Add(carryover);
            if (_options is { MaxBatchTokens: not null, TokenEstimator: not null })
                runningTokens += _options.TokenEstimator(carryover.Text);
        }

        while (batch.Count < _options.MaxBatchItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasMore = await enumerator.MoveNextAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!hasMore) return (batch, null);
            ValidateChunk(enumerator.Current);

            if (_options is { MaxBatchTokens: not null, TokenEstimator: not null } && batch.Count > 0)
            {
                var tokens = _options.TokenEstimator(enumerator.Current.Text);
                if (runningTokens + tokens > _options.MaxBatchTokens.Value)
                    return (batch, enumerator.Current);
                runningTokens += tokens;
            }
            else if (_options is { MaxBatchTokens: not null, TokenEstimator: not null })
            {
                runningTokens += _options.TokenEstimator(enumerator.Current.Text);
            }

            batch.Add(enumerator.Current);
        }

        return (batch, null);
    }

    private static void ValidateChunk(TextChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunk.DocumentId);
        ArgumentNullException.ThrowIfNull(chunk.Text);
        ArgumentOutOfRangeException.ThrowIfNegative(chunk.Index);
        ArgumentOutOfRangeException.ThrowIfNegative(chunk.StartOffset);
    }

    private static string? Validate(EmbeddingResponse response, int count, int? expectedDimensions)
    {
        if (response.Embeddings is null || response.Embeddings.Count != count)
        {
            if (response.Base64Embeddings is { Count: > 0 })
                return "Embedding response contains base64-encoded vectors. BulkEmbeddingProcessor requires float encoding; remove any ExtraParameters override of encoding_format.";
            return "Embedding response vector count does not match the submitted chunk count.";
        }
        foreach (var vector in response.Embeddings)
        {
            if (vector is null || vector.Length == 0)
                return "Embedding response contains an empty vector.";
            expectedDimensions ??= vector.Length;
            if (vector.Length != expectedDimensions)
                return "Embedding response vector dimensions are inconsistent with the requested dimensions or previous vectors.";
            if (vector.Any(value => !float.IsFinite(value)))
                return "Embedding response contains a non-finite vector value.";
        }
        return null;
    }

    private static async IAsyncEnumerable<TextChunk> AsAsync(
        IEnumerable<TextChunk> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        using var enumerator = chunks.GetEnumerator();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!enumerator.MoveNext()) yield break;
            cancellationToken.ThrowIfCancellationRequested();
            yield return enumerator.Current;
        }
    }

    private sealed class DimensionState
    {
        private readonly int? _configured;
        private int _inferred;

        public DimensionState(int? configured) => _configured = configured;

        public int? ExpectedDimensions
        {
            get
            {
                if (_configured is not null) return _configured;
                var inferred = Volatile.Read(ref _inferred);
                return inferred > 0 ? inferred : null;
            }
        }

        public void TrySetInferred(int dims) => Interlocked.CompareExchange(ref _inferred, dims, 0);
    }
}
