using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;
using NSubstitute;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class BulkEmbeddingProcessorTests
{
    private static readonly float[] SingleOneVector = [1f];
    private static readonly int[] ExpectedDefaultBatchSizes = [32, 1];
    private static TextChunk Chunk(int index) => new("document", index, index, index.ToString());
    private static EmbeddingResponse Response(params float[][] vectors) =>
        new(vectors, null, "model", 42, RawResponseJson: "response", RawRequestJson: "request");

    private static IEmbeddingClient Client(Func<EmbeddingRequest, EmbeddingResponse> respond)
    {
        var client = Substitute.For<IEmbeddingClient>();
        client.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(respond(call.Arg<EmbeddingRequest>())));
        return client;
    }

    private static async Task<List<EmbeddingBatchResult>> Collect(IAsyncEnumerable<EmbeddingBatchResult> batches)
    {
        var result = new List<EmbeddingBatchResult>();
        await foreach (var batch in batches) result.Add(batch);
        return result;
    }

    [Test]
    public async Task TenThousandChunks_AreBoundedOrderedAndAssociated()
    {
        var consumed = 0;
        IEnumerable<TextChunk> Source()
        {
            for (var index = 0; index < 10_000; index++)
            {
                consumed++;
                yield return Chunk(index);
            }
        }
        var client = Client(request => Response(request.Input.Select(text => new[] { float.Parse(text) }).ToArray()));
        var processor = new BulkEmbeddingProcessor(client, new() { BatchSize = 32 });
        var emitted = 0;
        long index = 0;
        await foreach (var batch in processor.EmbedAsync(Source()))
        {
            Assert.That(batch.BatchIndex, Is.EqualTo(index++));
            Assert.That(batch.Chunks, Has.Count.LessThanOrEqualTo(32));
            foreach (var item in batch.Items)
            {
                Assert.That(item.Chunk.Index, Is.EqualTo(emitted));
                Assert.That(item.Vector[0], Is.EqualTo((float)emitted++));
            }
            Assert.That(consumed, Is.EqualTo(emitted), "No read ahead beyond the yielded batch");
        }
        Assert.That(emitted, Is.EqualTo(10_000));
    }

    [Test]
    public async Task Options_AreSnapshottedAndForwardedIncludingClonedJson()
    {
        using var json = JsonDocument.Parse("{\"custom\":7}");
        var options = new BulkEmbeddingOptions
        {
            BatchSize = 1, Model = "chosen", Dimensions = 2, InputType = EmbeddingInputType.Document,
            IncludeRawResponse = true, ExtraParameters = json.RootElement
        };
        EmbeddingRequest? observed = null;
        var processor = new BulkEmbeddingProcessor(Client(request => { observed = request; return Response([1, 2]); }), options);
        options.BatchSize = 50;
        options.Model = "changed";
        json.Dispose();
        await Collect(processor.EmbedAsync(new[] { Chunk(0) }));
        Assert.Multiple(() =>
        {
            Assert.That(observed!.Model, Is.EqualTo("chosen"));
            Assert.That(observed.Dimensions, Is.EqualTo(2));
            Assert.That(observed.InputType, Is.EqualTo(EmbeddingInputType.Document));
            Assert.That(observed.EncodingFormat, Is.EqualTo("float"));
            Assert.That(observed.IncludeRawResponse, Is.True);
            Assert.That(observed.ExtraParameters!.Value.GetProperty("custom").GetInt32(), Is.EqualTo(7));
        });
    }

    [Test]
    public async Task ProviderFailure_IsPreservedAndStopsSource()
    {
        var failure = Response() with { IsSuccess = false, ErrorMessage = "rate limited" };
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ => ++calls == 1 ? Response([1]) : failure), new() { BatchSize = 1 });
        var results = await Collect(processor.EmbedAsync(Enumerable.Range(0, 5).Select(Chunk)));
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].IsSuccess, Is.True);
        Assert.That(results[1].Response, Is.SameAs(failure));
        Assert.That(results[1].ErrorMessage, Is.EqualTo("rate limited"));
        Assert.That(results[1].Items, Is.Empty);
        Assert.That(calls, Is.EqualTo(2));
    }

    [TestCase("count")]
    [TestCase("empty")]
    [TestCase("nan")]
    [TestCase("infinity")]
    [TestCase("inconsistent")]
    [TestCase("dimensions")]
    [TestCase("nullvector")]
    public async Task MalformedSuccess_YieldsFailureWithOriginalMetadata(string kind)
    {
        float[][] vectors = kind switch
        {
            "count" => [[1]], "empty" => [[], [1]], "nan" => [[float.NaN], [1]],
            "infinity" => [[float.PositiveInfinity], [1]], "inconsistent" => [[1], [1, 2]],
            "nullvector" => [null!, [1]], _ => [[1], [2]]
        };
        var original = Response(vectors);
        var processor = new BulkEmbeddingProcessor(Client(_ => original), new() { BatchSize = 2, Dimensions = kind == "dimensions" ? 2 : null });
        var batches = await Collect(processor.EmbedAsync(Enumerable.Range(0, 4).Select(Chunk)));
        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(batches[0].IsSuccess, Is.False);
            Assert.That(batches[0].ErrorMessage, Is.Not.Empty);
            Assert.That(batches[0].Items, Is.Empty);
            Assert.That(batches[0].Response.RawResponseJson, Is.EqualTo("response"));
            Assert.That(batches[0].Response.RawRequestJson, Is.EqualTo("request"));
            Assert.That(batches[0].Response.Model, Is.EqualTo("model"));
            Assert.That(batches[0].Response.TotalTokens, Is.EqualTo(42));
        });
    }

    [Test]
    public async Task DimensionChangeBetweenBatches_IsRejected()
    {
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ => ++calls == 1 ? Response([1]) : Response([1, 2])), new() { BatchSize = 1 });
        var batches = await Collect(processor.EmbedAsync(Enumerable.Range(0, 3).Select(Chunk)));
        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.That(batches[1].IsSuccess, Is.False);
    }

    [Test]
    public async Task EmptyInput_DoesNotCallProvider()
    {
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ => { calls++; return Response(); }));
        Assert.That(await Collect(processor.EmbedAsync(Array.Empty<TextChunk>())), Is.Empty);
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public async Task Defaults_UseDocumentInputAndBatchesOf32()
    {
        var requests = new List<EmbeddingRequest>();
        var processor = new BulkEmbeddingProcessor(Client(request =>
        {
            requests.Add(request);
            return Response(request.Input.Select(_ => SingleOneVector).ToArray());
        }));
        await Collect(processor.EmbedAsync(Enumerable.Range(0, 33).Select(Chunk)));
        Assert.That(requests.Select(request => request.Input.Count), Is.EqualTo(ExpectedDefaultBatchSizes));
        Assert.That(requests[0].InputType, Is.EqualTo(EmbeddingInputType.Document));
        Assert.That(requests[0].IncludeRawResponse, Is.False);
        Assert.That(requests[0].EncodingFormat, Is.EqualTo("float"));
    }

    [Test]
    public async Task Failure_DisposesAsyncSource()
    {
        var disposed = false;
        async IAsyncEnumerable<TextChunk> Source()
        {
            try
            {
                await Task.Yield();
                yield return Chunk(0);
                Assert.Fail("The source must stop after the failed batch.");
            }
            finally { disposed = true; }
        }
        var processor = new BulkEmbeddingProcessor(Client(_ => EmbeddingResponse.Error("failure")), new() { BatchSize = 1 });
        var batches = await Collect(processor.EmbedAsync(Source()));
        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(disposed, Is.True);
    }

    [Test]
    public void PreCanceledToken_DoesNotEnumerateSourceOrCallProvider()
    {
        IEnumerable<TextChunk> Source()
        {
            Assert.Fail("Canceled processing must not enumerate input.");
            yield return Chunk(0);
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var processor = new BulkEmbeddingProcessor(Client(_ => throw new AssertionException("Provider should not be called")));
        Assert.ThrowsAsync<OperationCanceledException>(() => Collect(processor.EmbedAsync(Source(), cancellation.Token)));
    }

    [Test]
    public async Task Dimensions_AreInferredIndependentlyForEachEnumeration()
    {
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ => ++calls == 1 ? Response([1]) : Response([1, 2])));
        var first = await Collect(processor.EmbedAsync(new[] { Chunk(0) }));
        var second = await Collect(processor.EmbedAsync(new[] { Chunk(0) }));
        Assert.That(first[0].IsSuccess, Is.True);
        Assert.That(second[0].IsSuccess, Is.True);
    }

    [Test]
    public async Task ConsumerBreak_DisposesAsyncInputWithoutReadAhead()
    {
        var disposed = false;
        var consumed = 0;
        async IAsyncEnumerable<TextChunk> Source()
        {
            try
            {
                await Task.Yield();
                for (var i = 0; i < 10; i++) { consumed++; yield return Chunk(i); }
            }
            finally { disposed = true; }
        }
        var processor = new BulkEmbeddingProcessor(Client(_ => Response([1], [2])), new() { BatchSize = 2 });
        await foreach (var _ in processor.EmbedAsync(Source())) break;
        Assert.That(disposed, Is.True);
        Assert.That(consumed, Is.EqualTo(2));
    }

    [Test]
    public void CancellationSwallowedByProvider_StillThrowsAndDisposesInput()
    {
        using var cancellation = new CancellationTokenSource();
        var disposed = false;
        IEnumerable<TextChunk> Source()
        {
            try { yield return Chunk(0); yield return Chunk(1); }
            finally { disposed = true; }
        }
        var processor = new BulkEmbeddingProcessor(Client(_ => { cancellation.Cancel(); return Response([1]); }), new() { BatchSize = 1 });
        Assert.ThrowsAsync<OperationCanceledException>(() => Collect(processor.EmbedAsync(Source(), cancellation.Token)));
        Assert.That(disposed, Is.True);
    }

    [Test]
    public void ProviderNetworkException_Propagates()
    {
        var processor = new BulkEmbeddingProcessor(Client(_ => throw new HttpRequestException("offline")));
        Assert.ThrowsAsync<HttpRequestException>(() => Collect(processor.EmbedAsync(new[] { Chunk(0) })));
    }

    [TestCase(0, null)]
    [TestCase(-1, null)]
    [TestCase(1, 0)]
    [TestCase(1, -1)]
    public void InvalidOptions_ThrowAtConstruction(int batchSize, int? dimensions)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BulkEmbeddingProcessor(Client(_ => Response()), new() { BatchSize = batchSize, Dimensions = dimensions }));
    }

    [Test]
    [TestCase(null, "text", 0, 0)]
    [TestCase(" ", "text", 0, 0)]
    [TestCase("doc", null, 0, 0)]
    [TestCase("doc", "text", -1, 0)]
    [TestCase("doc", "text", 0, -1)]
    public void InvalidChunkMetadata_ThrowsBeforeProviderTraffic(string? documentId, string? text, int index, int offset)
    {
        var client = Client(_ => throw new AssertionException("Invalid input must not be sent"));
        var processor = new BulkEmbeddingProcessor(client);
        Assert.CatchAsync<ArgumentException>(() => Collect(processor.EmbedAsync(
            new[] { new TextChunk(documentId!, index, offset, text!) })));
        client.DidNotReceive().GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void InvalidInputType_ThrowsAtConstruction() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new BulkEmbeddingProcessor(Client(_ => Response()), new() { InputType = (EmbeddingInputType)999 }));
}
