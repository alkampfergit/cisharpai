using Cisharpai.Models;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;
using Cisharpai.Rag.Pipeline;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.Pipeline;

[TestFixture]
public class RagPipelineStreamingTests
{
    private static ScoredChunk Scored(string docId, int index, string text, double score) =>
        new(new TextChunk(docId, index, 0, text.Length, text), score);

    private static IReadOnlyList<ScoredChunk> SampleChunks() =>
    [
        Scored("doc1", 0, "The capital of France is Paris.", 0.9),
        Scored("doc1", 1, "Paris is known for the Eiffel Tower.", 0.8)
    ];

    [Test]
    public async Task AskStreamingAsync_WithStreamingFeature_StreamsChunks()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };
        var chatClient = new FakeChatCompletionClient();
        chatClient.DefaultStreamingResponse = FakeResponses.StreamingChunks("The ", "capital ", "is Paris.");

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithChatClient(chatClient)
            .Build();

        var chunks = new List<RagStreamingChunk>();
        await foreach (var chunk in pipeline.AskStreamingAsync("What is the capital?"))
        {
            chunks.Add(chunk);
        }

        Assert.That(chunks, Has.Count.EqualTo(3));
        Assert.That(chunks[0].ContentDelta, Is.EqualTo("The "));
        Assert.That(chunks[0].FinishReason, Is.Null);
        Assert.That(chunks[2].FinishReason, Is.EqualTo("stop"));
        Assert.That(chunks[2].FinalResult, Is.Not.Null);
        Assert.That(chunks[2].FinalResult!.Answer, Is.EqualTo("The capital is Paris."));
        Assert.That(chunks[2].FinalResult.RetrievedChunks, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AskStreamingAsync_NoStreamingFeature_FallsBackToSingleChunk()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };
        var chatClient = new FakeChatCompletionClient(FakeChatFeatures.None);
        chatClient.DefaultResponse = FakeResponses.Chat("The capital is Paris.");

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithChatClient(chatClient)
            .Build();

        var chunks = new List<RagStreamingChunk>();
        await foreach (var chunk in pipeline.AskStreamingAsync("What is the capital?"))
        {
            chunks.Add(chunk);
        }

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0].ContentDelta, Is.EqualTo("The capital is Paris."));
        Assert.That(chunks[0].FinalResult, Is.Not.Null);
        Assert.That(chunks[0].FinalResult!.IsSuccess, Is.True);
    }

    [Test]
    public async Task AskStreamingAsync_NoChatClient_ReturnsSingleChunkWithEmptyAnswer()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .Build();

        var chunks = new List<RagStreamingChunk>();
        await foreach (var chunk in pipeline.AskStreamingAsync("test"))
        {
            chunks.Add(chunk);
        }

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0].FinalResult, Is.Not.Null);
        Assert.That(chunks[0].FinalResult!.Answer, Is.Empty);
        Assert.That(chunks[0].FinalResult.RetrievedChunks, Has.Count.EqualTo(2));
    }

    [Test]
    public void AskStreamingAsync_NullQuery_Throws()
    {
        var pipeline = new RagPipelineBuilder()
            .WithRetriever(FakeResponses.Retriever())
            .Build();

        Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in pipeline.AskStreamingAsync(null!)) { }
        });
    }
}
