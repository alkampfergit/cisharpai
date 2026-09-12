using Cisharpai.Models;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;
using NSubstitute;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class BulkEmbeddingProviderProfileTests
{
    private static TextChunk Chunk(int index) => new("document", index, index, index.ToString());

    private static EmbeddingResponse Response(int count) =>
        new(Enumerable.Range(0, count).Select(_ => new[] { 1f }).ToArray(), null, "model", 42);

    [TestCase(EmbeddingProviderProfile.Conservative, 32, null)]
    [TestCase(EmbeddingProviderProfile.OpenAi, 96, 250_000)]
    [TestCase(EmbeddingProviderProfile.AzureOpenAi, 16, 100_000)]
    [TestCase(EmbeddingProviderProfile.AzureAiInference, 64, 100_000)]
    [TestCase(EmbeddingProviderProfile.Cohere, 96, 100_000)]
    public void ForProvider_SetsProviderCeilings(EmbeddingProviderProfile profile, int expectedItems, int? expectedTokens)
    {
        var options = BulkEmbeddingOptions.ForProvider(profile);

        Assert.That(options.MaxBatchItems, Is.EqualTo(expectedItems));
        Assert.That(options.MaxBatchTokens, Is.EqualTo(expectedTokens));
    }

    [Test]
    public void ForProvider_LeavesOtherDefaultsUntouched()
    {
        var options = BulkEmbeddingOptions.ForProvider(EmbeddingProviderProfile.OpenAi);

        Assert.That(options.MaxConcurrency, Is.EqualTo(1));
        Assert.That(options.MaxRetries, Is.EqualTo(3));
        Assert.That(options.InputType, Is.EqualTo(EmbeddingInputType.Document));
        Assert.That(options.TokenEstimator, Is.Not.Null);
    }

    [Test]
    public void ApplyProfile_OverwritesOnlyBatchCeilingsAndReturnsSameInstance()
    {
        var options = new BulkEmbeddingOptions { MaxBatchItems = 7, MaxBatchTokens = 11, Model = "custom", MaxConcurrency = 4 };

        var returned = options.ApplyProfile(EmbeddingProviderProfile.Cohere);

        Assert.That(returned, Is.SameAs(options));
        Assert.That(options.MaxBatchItems, Is.EqualTo(96));
        Assert.That(options.MaxBatchTokens, Is.EqualTo(100_000));
        Assert.That(options.Model, Is.EqualTo("custom"));
        Assert.That(options.MaxConcurrency, Is.EqualTo(4));
    }

    [Test]
    public void UnknownProfile_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BulkEmbeddingOptions.ForProvider((EmbeddingProviderProfile)99));

    [Test]
    public async Task ProfileCeiling_IsAppliedByTheProcessor()
    {
        var requests = new List<EmbeddingRequest>();
        var client = Substitute.For<IEmbeddingClient>();
        client.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<EmbeddingRequest>();
                requests.Add(request);
                return Task.FromResult(Response(request.Input.Count));
            });

        var processor = new BulkEmbeddingProcessor(client, BulkEmbeddingOptions.ForProvider(EmbeddingProviderProfile.Cohere));
        await foreach (var _ in processor.EmbedAsync(Enumerable.Range(0, 100).Select(Chunk))) { }

        Assert.That(requests.Select(r => r.Input.Count), Is.EqualTo(new[] { 96, 4 }));
    }
}
