using System.Runtime.CompilerServices;
using Cisharpai.Models;
using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Embeddings;

/// <summary>
/// Embeds one bounded batch at a time. A provider or validation failure is yielded once,
/// then processing stops; cancellation and transport exceptions propagate to the caller.
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
        IEnumerable<TextChunk> chunks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return EmbedCoreAsync(AsAsync(chunks, cancellationToken), cancellationToken);
    }

    public IAsyncEnumerable<EmbeddingBatchResult> EmbedAsync(
        IAsyncEnumerable<TextChunk> chunks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return EmbedCoreAsync(chunks, cancellationToken);
    }

    private async IAsyncEnumerable<EmbeddingBatchResult> EmbedCoreAsync(
        IAsyncEnumerable<TextChunk> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var enumerator = chunks.GetAsyncEnumerator(cancellationToken);
        long batchIndex = 0;
        int? expectedDimensions = _options.Dimensions;
        while (true)
        {
            var batch = await CollectBatchAsync(enumerator, _options.BatchSize, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (batch.Count == 0) yield break;
            var request = new EmbeddingRequest(
                batch.Select(chunk => chunk.Text).ToArray(),
                Model: _options.Model,
                InputType: _options.InputType,
                Dimensions: _options.Dimensions,
                EncodingFormat: "float",
                IncludeRawResponse: _options.IncludeRawResponse,
                ExtraParameters: _options.ExtraParameters);
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

            IReadOnlyList<ChunkEmbedding> items = response.IsSuccess
                ? batch.Select((chunk, index) => new ChunkEmbedding(chunk, response.Embeddings[index])).ToArray()
                : Array.Empty<ChunkEmbedding>();
            yield return new EmbeddingBatchResult(batchIndex++, batch.AsReadOnly(), items, response);
            if (!response.IsSuccess) yield break;
        }
    }

    private static async Task<List<TextChunk>> CollectBatchAsync(
        IAsyncEnumerator<TextChunk> enumerator, int batchSize, CancellationToken cancellationToken)
    {
        var batch = new List<TextChunk>();
        while (batch.Count < batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) break;
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChunk(enumerator.Current);
            batch.Add(enumerator.Current);
        }
        return batch;
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
            return "Embedding response vector count does not match the submitted chunk count.";
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
}
