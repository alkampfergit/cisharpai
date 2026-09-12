using System.Runtime.CompilerServices;
using Cisharpai.Models;
using Cisharpai.Rag;
using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;
using Cisharpai.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class RagIngestionPipelineTests
{
    private static readonly int[] ExpectedBatchItemCounts = [3, 1];
    private static readonly string[] ExpectedChunkTexts = ["ab", "cd", "ef", "gh"];
    private static readonly string[] ExpectedDocumentIds = ["a", "a", "b", "b"];
    private static readonly int[] ExpectedChunkIndices = [0, 1, 0, 1];
    private static readonly string[] ExpectedBoundInput = ["ab", "cd"];
    [Test]
    public async Task Ingest_BatchesAcrossDocumentsAndPreservesIdentity()
    {
        var fake = new FakeEmbeddingClient();
        fake.EnqueueResponse(Response(3));
        fake.EnqueueResponse(Response(1));
        var pipeline = new RagIngestionPipeline(
            new FixedSizeChunker(new() { ChunkSize = 2, Overlap = 0 }),
            new BulkEmbeddingProcessor(fake, new() { MaxBatchItems = 3 }));

        var batches = await Collect(pipeline.IngestAsync(new[]
        {
            new RagDocument("a", "abcd"), new RagDocument("empty", ""), new RagDocument("b", "efgh")
        }));

        Assert.Multiple(() =>
        {
            Assert.That(batches.Select(b => b.Items.Count), Is.EqualTo(ExpectedBatchItemCounts));
            Assert.That(batches.SelectMany(b => b.Chunks).Select(c => c.Text), Is.EqualTo(ExpectedChunkTexts));
            Assert.That(batches.SelectMany(b => b.Chunks).Select(c => c.DocumentId), Is.EqualTo(ExpectedDocumentIds));
            Assert.That(batches.SelectMany(b => b.Chunks).Select(c => c.Index), Is.EqualTo(ExpectedChunkIndices));
        });
    }

    [Test]
    public async Task AddCisharpaiRag_ConfiguresPipelineAndUsesScopedProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IEmbeddingClient>(_ => new FakeEmbeddingClient { DefaultResponse = Response(2) });
        services.AddCisharpaiRag(options =>
        {
            options.Chunking.ChunkSize = 2;
            options.Chunking.Overlap = 0;
            options.Embedding.MaxBatchItems = 2;
            options.Embedding.Model = "configured-model";
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IRagIngestionPipeline>();
        var batches = await Collect(pipeline.IngestAsync(new[] { new RagDocument("a", "abcd") }));
        var fake = (FakeEmbeddingClient)scope.ServiceProvider.GetRequiredService<IEmbeddingClient>();
        Assert.That(batches.Single().Items, Has.Count.EqualTo(2));
        Assert.That(fake.ReceivedRequests.Single().Model, Is.EqualTo("configured-model"));

        using var secondScope = provider.CreateScope();
        Assert.That(secondScope.ServiceProvider.GetRequiredService<IRagIngestionPipeline>(), Is.Not.SameAs(pipeline));
        Assert.That(secondScope.ServiceProvider.GetRequiredService<IEmbeddingClient>(), Is.Not.SameAs(fake));
    }

    [Test]
    public async Task AddCisharpaiRag_SelectsKeyedProvider()
    {
        var selected = new FakeEmbeddingClient { DefaultResponse = Response(1) };
        var other = new FakeEmbeddingClient();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IEmbeddingClient>("selected", selected);
        services.AddSingleton<IEmbeddingClient>(other);
        services.AddCisharpaiRag(sp => sp.GetRequiredKeyedService<IEmbeddingClient>("selected"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var batches = await Collect(scope.ServiceProvider.GetRequiredService<IRagIngestionPipeline>()
            .IngestAsync(new[] { new RagDocument("a", "text") }));
        Assert.That(batches.Single().IsSuccess, Is.True);
        Assert.That(selected.CallCount, Is.EqualTo(1));
        Assert.That(other.CallCount, Is.Zero);
    }

    [Test]
    public async Task AddCisharpaiRag_BindsHostConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Rag:Chunking:ChunkSize"] = "2",
            ["Rag:Chunking:Overlap"] = "0",
            ["Rag:Embedding:MaxBatchItems"] = "2",
            ["Rag:Embedding:Model"] = "bound-model",
            ["Rag:Embedding:Dimensions"] = "2",
            ["Rag:Embedding:InputType"] = "Document",
            ["Rag:Embedding:IncludeRawResponse"] = "true"
        }).Build();
        var fake = new FakeEmbeddingClient { DefaultResponse = Response(2) };
        var services = new ServiceCollection();
        services.AddSingleton<IEmbeddingClient>(fake);
        services.AddCisharpaiRag(options => configuration.GetSection("Rag").Bind(options));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var batches = await Collect(scope.ServiceProvider.GetRequiredService<IRagIngestionPipeline>()
            .IngestAsync(new[] { new RagDocument("bound", "abcd") }));
        Assert.That(batches.Single().IsSuccess, Is.True);
        var request = fake.ReceivedRequests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Input, Is.EqualTo(ExpectedBoundInput));
            Assert.That(request.Model, Is.EqualTo("bound-model"));
            Assert.That(request.Dimensions, Is.EqualTo(2));
            Assert.That(request.InputType, Is.EqualTo(EmbeddingInputType.Document));
            Assert.That(request.IncludeRawResponse, Is.True);
        });
    }

    [Test]
    public void AddCisharpaiRag_RejectsInvalidNestedOptionsBeforeTraffic()
    {
        var fake = new FakeEmbeddingClient();
        var services = new ServiceCollection();
        services.AddSingleton<IEmbeddingClient>(fake);
        services.AddCisharpaiRag(options => options.Chunking.ChunkSize = 0);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Throws<ArgumentOutOfRangeException>(() => scope.ServiceProvider.GetRequiredService<IRagIngestionPipeline>());
        Assert.That(fake.CallCount, Is.Zero);
    }

    [Test]
    public async Task Ingest_EarlyDisposalDoesNotReadAheadAndDisposesSource()
    {
        var visited = 0;
        var disposed = false;
        IEnumerable<RagDocument> Source()
        {
            try
            {
                for (var i = 0; i < 100; i++)
                {
                    visited++;
                    yield return new(i.ToString(), "ab");
                }
            }
            finally { disposed = true; }
        }

        var fake = new FakeEmbeddingClient { DefaultResponse = Response(2) };
        var pipeline = new RagIngestionPipeline(new FixedSizeChunker(), new BulkEmbeddingProcessor(fake, new() { MaxBatchItems = 2 }));
        await foreach (var batch in pipeline.IngestAsync(Source()))
        {
            Assert.That(batch.Items, Has.Count.EqualTo(2));
            Assert.That(visited, Is.EqualTo(2));
            break;
        }
        Assert.That(disposed, Is.True);
        Assert.That(fake.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Ingest_AsyncSourceReceivesCancellationAndIsDisposed()
    {
        using var cancellation = new CancellationTokenSource();
        var disposed = false;
        var observedToken = CancellationToken.None;
        async IAsyncEnumerable<RagDocument> Source([EnumeratorCancellation] CancellationToken token = default)
        {
            observedToken = token;
            try
            {
                await Task.CompletedTask;
                yield return new("a", "ab");
                token.ThrowIfCancellationRequested();
                yield return new("b", "cd");
            }
            finally { disposed = true; }
        }

        var fake = new FakeEmbeddingClient { DefaultResponse = Response(1) };
        var pipeline = new RagIngestionPipeline(new FixedSizeChunker(), new BulkEmbeddingProcessor(fake, new() { MaxBatchItems = 1 }));
        await using var enumerator = pipeline.IngestAsync(Source(), null, cancellation.Token).GetAsyncEnumerator();
        Assert.That(await enumerator.MoveNextAsync(), Is.True);
        cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        Assert.That(observedToken, Is.EqualTo(cancellation.Token));
        Assert.That(disposed, Is.True);
        Assert.That(fake.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Ingest_ConsumerCancellationInterruptsEmptySynchronousDocuments()
    {
        using var cancellation = new CancellationTokenSource();
        var disposed = false;
        IEnumerable<RagDocument> Source()
        {
            try
            {
                cancellation.Cancel();
                yield return new("empty", "");
                throw new InvalidOperationException("Must not advance after cancellation");
            }
            finally { disposed = true; }
        }
        var fake = new FakeEmbeddingClient();
        var pipeline = new RagIngestionPipeline(new FixedSizeChunker(), new BulkEmbeddingProcessor(fake));
        await using var enumerator = pipeline.IngestAsync(Source()).GetAsyncEnumerator(cancellation.Token);
        Assert.ThrowsAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        Assert.That(disposed, Is.True);
        Assert.That(fake.CallCount, Is.Zero);
    }

    [Test]
    public async Task Ingest_ProviderFailureSurfacedAndDisposesDocuments()
    {
        var disposed = false;
        IEnumerable<RagDocument> Source()
        {
            try { yield return new("a", "text"); }
            finally { disposed = true; }
        }
        var fake = new FakeEmbeddingClient { DefaultResponse = EmbeddingResponse.Error("quota") };
        var pipeline = new RagIngestionPipeline(new FixedSizeChunker(), new BulkEmbeddingProcessor(fake, new() { MaxBatchItems = 1 }));
        var batches = await Collect(pipeline.IngestAsync(Source()));
        Assert.That(batches.Single().ErrorMessage, Is.EqualTo("quota"));
        Assert.That(disposed, Is.True);
    }

    [Test]
    public void AddCisharpaiRag_PreservesCustomChunker()
    {
        var chunker = Substitute.For<ITextChunker>();
        var services = new ServiceCollection();
        services.AddSingleton(chunker);
        services.AddCisharpaiRag();
        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<ITextChunker>(), Is.SameAs(chunker));
    }

    [Test]
    public void AddCisharpaiRag_SecondRegistrationThrows()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmbeddingClient>(_ => new FakeEmbeddingClient());
        services.AddCisharpaiRag();
        Assert.Throws<InvalidOperationException>(() => services.AddCisharpaiRag(
            sp => sp.GetRequiredService<IEmbeddingClient>(),
            options => options.Embedding.MaxBatchItems = 1));
    }

    [Test]
    public void ConstructorsAndInputs_RejectNull()
    {
        var chunker = new FixedSizeChunker();
        var processor = new BulkEmbeddingProcessor(new FakeEmbeddingClient());
        Assert.Throws<ArgumentNullException>(() => new RagIngestionPipeline(null!, processor));
        Assert.Throws<ArgumentNullException>(() => new RagIngestionPipeline(chunker, null!));
        var pipeline = new RagIngestionPipeline(chunker, processor);
        Assert.Throws<ArgumentNullException>(() => pipeline.IngestAsync((IEnumerable<RagDocument>)null!));
        Assert.Throws<ArgumentNullException>(() => pipeline.IngestAsync((IAsyncEnumerable<RagDocument>)null!));
    }

    private static EmbeddingResponse Response(int count) =>
        new(Enumerable.Range(0, count).Select(i => new[] { (float)i, 1f }).ToArray(), null, "fake", count);

    private static async Task<List<EmbeddingBatchResult>> Collect(IAsyncEnumerable<EmbeddingBatchResult> source)
    {
        var results = new List<EmbeddingBatchResult>();
        await foreach (var result in source) results.Add(result);
        return results;
    }
}
