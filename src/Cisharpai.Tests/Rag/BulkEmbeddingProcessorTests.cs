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
    private static TextChunk ChunkWithText(int index, string text) => new("document", index, index, text);
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
        var processor = new BulkEmbeddingProcessor(client, new() { MaxBatchItems = 32 });
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
            MaxBatchItems = 1, Model = "chosen", Dimensions = 2, InputType = EmbeddingInputType.Document,
            IncludeRawResponse = true, ExtraParameters = json.RootElement
        };
        EmbeddingRequest? observed = null;
        var processor = new BulkEmbeddingProcessor(Client(request => { observed = request; return Response([1, 2]); }), options);
        options.MaxBatchItems = 50;
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
    public async Task PermanentFailure_SurfacedAndProcessingContinues()
    {
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ => ++calls == 2
            ? EmbeddingResponse.Error("permanent error")
            : Response([1])),
            new() { MaxBatchItems = 1, MaxRetries = 0 });
        var results = await Collect(processor.EmbedAsync(Enumerable.Range(0, 5).Select(Chunk)));
        Assert.That(results, Has.Count.EqualTo(5));
        Assert.That(results[0].IsSuccess, Is.True);
        Assert.That(results[1].IsSuccess, Is.False);
        Assert.That(results[1].ErrorMessage, Is.EqualTo("permanent error"));
        Assert.That(results[1].Items, Is.Empty);
        Assert.That(results[2].IsSuccess, Is.True);
        Assert.That(results[3].IsSuccess, Is.True);
        Assert.That(results[4].IsSuccess, Is.True);
        Assert.That(calls, Is.EqualTo(5));
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
        var processor = new BulkEmbeddingProcessor(Client(_ => original), new() { MaxBatchItems = 2, Dimensions = kind == "dimensions" ? 2 : null });
        var batches = await Collect(processor.EmbedAsync(Enumerable.Range(0, 4).Select(Chunk)));
        Assert.That(batches, Has.Count.EqualTo(2));
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
        var processor = new BulkEmbeddingProcessor(Client(_ => ++calls == 1 ? Response([1]) : Response([1, 2])), new() { MaxBatchItems = 1 });
        var batches = await Collect(processor.EmbedAsync(Enumerable.Range(0, 3).Select(Chunk)));
        Assert.That(batches, Has.Count.EqualTo(3));
        Assert.That(batches[0].IsSuccess, Is.True);
        Assert.That(batches[1].IsSuccess, Is.False);
        Assert.That(batches[2].IsSuccess, Is.False);
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
            }
            finally { disposed = true; }
        }
        var processor = new BulkEmbeddingProcessor(Client(_ => EmbeddingResponse.Error("failure")), new() { MaxBatchItems = 1 });
        var batches = await Collect(processor.EmbedAsync(Source()));
        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(batches[0].IsSuccess, Is.False);
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
        Assert.ThrowsAsync<OperationCanceledException>(() => Collect(processor.EmbedAsync(Source(), null, cancellation.Token)));
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
        var processor = new BulkEmbeddingProcessor(Client(_ => Response([1], [2])), new() { MaxBatchItems = 2 });
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
        var processor = new BulkEmbeddingProcessor(Client(_ => { cancellation.Cancel(); return Response([1]); }), new() { MaxBatchItems = 1 });
        Assert.ThrowsAsync<OperationCanceledException>(() => Collect(processor.EmbedAsync(Source(), null, cancellation.Token)));
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
    public void InvalidMaxBatchItemsOrDimensions_ThrowAtConstruction(int maxBatchItems, int? dimensions)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxBatchItems = maxBatchItems, Dimensions = dimensions }));
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

    [Test]
    public async Task Base64Response_YieldsExplicitError()
    {
        var base64Response = new EmbeddingResponse(
            Embeddings: [],
            Base64Embeddings: ["AAAA"],
            Model: "model",
            TotalTokens: 1);
        var processor = new BulkEmbeddingProcessor(Client(_ => base64Response), new() { MaxBatchItems = 1 });
        var batches = await Collect(processor.EmbedAsync(new[] { Chunk(0) }));
        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(batches[0].IsSuccess, Is.False);
            Assert.That(batches[0].ErrorMessage, Does.Contain("base64"));
        });
    }

    [Test]
    public async Task MoreVectorsThanChunks_IsRejected()
    {
        var processor = new BulkEmbeddingProcessor(Client(_ => Response([1], [2], [3])), new() { MaxBatchItems = 2 });
        var batches = await Collect(processor.EmbedAsync(new[] { Chunk(0), Chunk(1) }));
        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(batches[0].IsSuccess, Is.False);
            Assert.That(batches[0].ErrorMessage, Does.Contain("count"));
        });
    }

    // --- Token-budget batch splitting ---

    [Test]
    public async Task TokenBudget_ClosesBatchWhenExceeded()
    {
        var requests = new List<EmbeddingRequest>();
        var processor = new BulkEmbeddingProcessor(Client(request =>
        {
            requests.Add(request);
            return Response(request.Input.Select(_ => SingleOneVector).ToArray());
        }), new() { MaxBatchItems = 100, MaxBatchTokens = 10, TokenEstimator = s => s.Length });

        var chunks = new[]
        {
            ChunkWithText(0, "aaaa"),    // 4 tokens, running=4
            ChunkWithText(1, "bbbb"),    // 4 tokens, running=8
            ChunkWithText(2, "cccccc"),  // 6 tokens, would push to 14 → carryover
            ChunkWithText(3, "dd"),      // 2 tokens
        };
        var batches = await Collect(processor.EmbedAsync(chunks));

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.That(requests[0].Input.Count, Is.EqualTo(2));
        Assert.That(requests[1].Input.Count, Is.EqualTo(2));
        Assert.That(requests[0].Input[0], Is.EqualTo("aaaa"));
        Assert.That(requests[0].Input[1], Is.EqualTo("bbbb"));
        Assert.That(requests[1].Input[0], Is.EqualTo("cccccc"));
        Assert.That(requests[1].Input[1], Is.EqualTo("dd"));
    }

    [Test]
    public async Task TokenBudget_FirstChunkAlwaysIncludedRegardlessOfBudget()
    {
        var requests = new List<EmbeddingRequest>();
        var processor = new BulkEmbeddingProcessor(Client(request =>
        {
            requests.Add(request);
            return Response(request.Input.Select(_ => SingleOneVector).ToArray());
        }), new() { MaxBatchItems = 100, MaxBatchTokens = 5, TokenEstimator = s => s.Length });

        var batches = await Collect(processor.EmbedAsync(new[]
        {
            ChunkWithText(0, "this-exceeds-budget"),
            ChunkWithText(1, "ab"),
        }));

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.That(requests[0].Input.Count, Is.EqualTo(1));
        Assert.That(requests[0].Input[0], Is.EqualTo("this-exceeds-budget"));
        Assert.That(requests[1].Input.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task TokenBudget_ItemCeilingStillRespected()
    {
        var requests = new List<EmbeddingRequest>();
        var processor = new BulkEmbeddingProcessor(Client(request =>
        {
            requests.Add(request);
            return Response(request.Input.Select(_ => SingleOneVector).ToArray());
        }), new() { MaxBatchItems = 2, MaxBatchTokens = 10000, TokenEstimator = s => s.Length });

        var batches = await Collect(processor.EmbedAsync(Enumerable.Range(0, 5).Select(Chunk)));
        Assert.That(requests[0].Input.Count, Is.EqualTo(2));
        Assert.That(requests[1].Input.Count, Is.EqualTo(2));
        Assert.That(requests[2].Input.Count, Is.EqualTo(1));
    }

    // --- Retry ---

    [Test]
    public async Task TransientFailure_RetriedAndEventuallySucceeds()
    {
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ =>
        {
            calls++;
            return calls <= 2
                ? EmbeddingResponse.Error("429 rate limited")
                : Response([1]);
        }), new() { MaxBatchItems = 1, MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMilliseconds(1) });

        var results = await Collect(processor.EmbedAsync(new[] { Chunk(0) }));
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].IsSuccess, Is.True);
        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public async Task TransientFailure_ExhaustedRetries_SurfacedAndContinues()
    {
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ =>
        {
            calls++;
            return calls <= 4
                ? EmbeddingResponse.Error("503 service unavailable")
                : Response([1]);
        }), new() { MaxBatchItems = 1, MaxRetries = 2, RetryBaseDelay = TimeSpan.FromMilliseconds(1) });

        var results = await Collect(processor.EmbedAsync(Enumerable.Range(0, 2).Select(Chunk)));
        Assert.That(results[0].IsSuccess, Is.False);
        Assert.That(results[0].ErrorMessage, Does.Contain("503"));
        Assert.That(results[1].IsSuccess, Is.True);
    }

    [Test]
    public async Task NonTransientFailure_NotRetried()
    {
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ =>
        {
            calls++;
            return EmbeddingResponse.Error("invalid input");
        }), new() { MaxBatchItems = 1, MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMilliseconds(1) });

        var results = await Collect(processor.EmbedAsync(new[] { Chunk(0) }));
        Assert.That(results[0].IsSuccess, Is.False);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public async Task CustomIsTransientError_OverridesDefault()
    {
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ =>
        {
            calls++;
            return calls == 1
                ? EmbeddingResponse.Error("custom-transient")
                : Response([1]);
        }), new()
        {
            MaxBatchItems = 1,
            MaxRetries = 3,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransientError = r => r.ErrorMessage?.Contains("custom-transient") == true
        });

        var results = await Collect(processor.EmbedAsync(new[] { Chunk(0) }));
        Assert.That(results[0].IsSuccess, Is.True);
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public async Task ZeroRetries_NoRetryAttempted()
    {
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ =>
        {
            calls++;
            return EmbeddingResponse.Error("429 rate limited");
        }), new() { MaxBatchItems = 1, MaxRetries = 0 });

        var results = await Collect(processor.EmbedAsync(new[] { Chunk(0) }));
        Assert.That(results[0].IsSuccess, Is.False);
        Assert.That(calls, Is.EqualTo(1));
    }

    // --- Default transient detection ---

    [TestCase("429 Too Many Requests", true)]
    [TestCase("rate limit exceeded", true)]
    [TestCase("too many requests", true)]
    [TestCase("request throttled", true)]
    [TestCase("500 Internal Server Error", true)]
    [TestCase("502 Bad Gateway", true)]
    [TestCase("503 Service Unavailable", true)]
    [TestCase("504 Gateway Timeout", true)]
    [TestCase("internal server error", true)]
    [TestCase("service unavailable", true)]
    [TestCase("bad gateway", true)]
    [TestCase("gateway timeout", true)]
    [TestCase("invalid input", false)]
    [TestCase("model not found", false)]
    [TestCase("unauthorized", false)]
    [TestCase("", false)]
    public void DefaultIsTransient_DetectsKnownPatterns(string errorMessage, bool expected)
    {
        var response = EmbeddingResponse.Error(errorMessage);
        Assert.That(BulkEmbeddingProcessor.DefaultIsTransient(response), Is.EqualTo(expected));
    }

    // --- Progress ---

    [Test]
    public async Task Progress_ReportedForEachBatch()
    {
        var reports = new List<BulkEmbeddingProgress>();
        var progress = new CapturingProgress<BulkEmbeddingProgress>(reports);
        var calls = 0;
        var processor = new BulkEmbeddingProcessor(Client(_ =>
        {
            calls++;
            return calls == 2 ? EmbeddingResponse.Error("fail") : Response([1]);
        }), new() { MaxBatchItems = 1, MaxRetries = 0 });

        await Collect(processor.EmbedAsync(Enumerable.Range(0, 3).Select(Chunk), progress));

        Assert.That(reports, Has.Count.EqualTo(3));
        Assert.That(reports[0], Is.EqualTo(new BulkEmbeddingProgress(1, 1, 0)));
        Assert.That(reports[1], Is.EqualTo(new BulkEmbeddingProgress(2, 2, 1)));
        Assert.That(reports[2], Is.EqualTo(new BulkEmbeddingProgress(3, 3, 1)));
    }

    private sealed class CapturingProgress<T>(List<T> reports) : IProgress<T>
    {
        public void Report(T value) => reports.Add(value);
    }

    // --- Concurrency ---

    [Test]
    public async Task Concurrency_OrderPreservedRegardlessOfCompletionOrder()
    {
        var gates = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource()).ToArray();
        var callIndex = 0;

        var client = Substitute.For<IEmbeddingClient>();
        client.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var idx = Interlocked.Increment(ref callIndex) - 1;
                return gates[idx].Task.ContinueWith(_ =>
                    Response(call.Arg<EmbeddingRequest>().Input.Select(t => new[] { float.Parse(t) }).ToArray()),
                    TaskScheduler.Default);
            });

        var processor = new BulkEmbeddingProcessor(client, new() { MaxBatchItems = 1, MaxConcurrency = 4 });

        var collectTask = Collect(processor.EmbedAsync(Enumerable.Range(0, 4).Select(Chunk)));

        await Task.Delay(100);

        // Complete in reverse order
        gates[3].SetResult();
        gates[2].SetResult();
        gates[1].SetResult();
        gates[0].SetResult();

        var results = await collectTask;

        Assert.That(results, Has.Count.EqualTo(4));
        for (int i = 0; i < 4; i++)
        {
            Assert.That(results[i].BatchIndex, Is.EqualTo(i));
            Assert.That(results[i].Items[0].Vector[0], Is.EqualTo((float)i));
        }
    }

    [Test]
    public async Task Concurrency_FailedBatchDoesNotStopOthers()
    {
        var calls = 0;
        var client = Substitute.For<IEmbeddingClient>();
        client.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var idx = Interlocked.Increment(ref calls);
                return Task.FromResult(idx == 2
                    ? EmbeddingResponse.Error("batch 1 failed")
                    : Response(call.Arg<EmbeddingRequest>().Input.Select(_ => SingleOneVector).ToArray()));
            });

        var processor = new BulkEmbeddingProcessor(client, new() { MaxBatchItems = 1, MaxConcurrency = 3, MaxRetries = 0 });
        var results = await Collect(processor.EmbedAsync(Enumerable.Range(0, 3).Select(Chunk)));

        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results[0].IsSuccess, Is.True);
        Assert.That(results[1].IsSuccess, Is.False);
        Assert.That(results[2].IsSuccess, Is.True);
    }

    // --- Options validation ---

    [Test]
    public void InvalidMaxBatchTokens_ThrowAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxBatchTokens = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxBatchTokens = -1 }));
    }

    [Test]
    public void TokenEstimatorNullWithMaxBatchTokens_ThrowAtConstruction()
    {
        Assert.Throws<ArgumentException>(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxBatchTokens = 100, TokenEstimator = null }));
    }

    [Test]
    public void InvalidMaxConcurrency_ThrowAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxConcurrency = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxConcurrency = 33 }));
    }

    [Test]
    public void InvalidMaxRetries_ThrowAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxRetries = -1 }));
    }

    [Test]
    public void InvalidRetryBaseDelay_ThrowAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { RetryBaseDelay = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { RetryBaseDelay = TimeSpan.FromSeconds(-1) }));
    }

    [Test]
    public void MaxBatchTokensWithoutTokenEstimator_UsesDefault()
    {
        Assert.DoesNotThrow(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxBatchTokens = 100 }));
    }

    [Test]
    public void MaxConcurrency32_IsValid()
    {
        Assert.DoesNotThrow(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxConcurrency = 32 }));
    }

    [Test]
    public void MaxRetries0_IsValid()
    {
        Assert.DoesNotThrow(() =>
            new BulkEmbeddingProcessor(Client(_ => Response()), new() { MaxRetries = 0 }));
    }
}
