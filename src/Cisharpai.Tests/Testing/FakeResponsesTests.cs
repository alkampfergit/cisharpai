using Cisharpai.Models;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Testing;

[TestFixture]
public class FakeResponsesTests
{
    [Test]
    public void Chat_CreatesSuccessfulResponse()
    {
        var response = FakeResponses.Chat("Hello world");

        Assert.That(response.Content, Is.EqualTo("Hello world"));
        Assert.That(response.Model, Is.EqualTo("fake-model"));
        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.PromptTokens, Is.EqualTo(10));
        Assert.That(response.CompletionTokens, Is.EqualTo(5));
    }

    [Test]
    public void Chat_WithCustomParameters()
    {
        var response = FakeResponses.Chat("Hi", model: "gpt-4", promptTokens: 100, completionTokens: 50);

        Assert.That(response.Model, Is.EqualTo("gpt-4"));
        Assert.That(response.PromptTokens, Is.EqualTo(100));
        Assert.That(response.CompletionTokens, Is.EqualTo(50));
    }

    [Test]
    public void ChatError_CreatesErrorResponse()
    {
        var response = FakeResponses.ChatError("rate limit exceeded");

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Is.EqualTo("rate limit exceeded"));
        Assert.That(response.Content, Is.Empty);
    }

    [Test]
    public void ToolCall_CreatesSingleToolCall()
    {
        var response = FakeResponses.ToolCall("get_weather", """{"city":"Paris"}""");

        Assert.That(response.ToolCalls, Has.Count.EqualTo(1));
        Assert.That(response.ToolCalls![0].FunctionName, Is.EqualTo("get_weather"));
        Assert.That(response.ToolCalls[0].Arguments.GetProperty("city").GetString(), Is.EqualTo("Paris"));
        Assert.That(response.IsSuccess, Is.True);
    }

    [Test]
    public void ToolCall_WithCustomId()
    {
        var response = FakeResponses.ToolCall("fn", "{}", id: "call_123");

        Assert.That(response.ToolCalls![0].Id, Is.EqualTo("call_123"));
    }

    [Test]
    public void ToolCalls_CreatesMultipleToolCalls()
    {
        var response = FakeResponses.ToolCalls(
            ("get_weather", """{"city":"Paris"}"""),
            ("get_time", """{"timezone":"UTC"}"""));

        Assert.That(response.ToolCalls, Has.Count.EqualTo(2));
        Assert.That(response.ToolCalls![0].FunctionName, Is.EqualTo("get_weather"));
        Assert.That(response.ToolCalls[1].FunctionName, Is.EqualTo("get_time"));
    }

    [Test]
    public void GroundedChat_CreatesResponse()
    {
        var response = FakeResponses.GroundedChat("answer with citations");

        Assert.That(response.Content, Is.EqualTo("answer with citations"));
        Assert.That(response.Citations, Is.Empty);
        Assert.That(response.IsSuccess, Is.True);
    }

    [Test]
    public void GroundedChat_WithCitations()
    {
        var citations = new List<Citation>
        {
            new(Start: 0, End: 5, Text: "Paris", Sources: [])
        };
        var response = FakeResponses.GroundedChat("Paris is great", citations);

        Assert.That(response.Citations, Has.Count.EqualTo(1));
        Assert.That(response.Citations[0].Text, Is.EqualTo("Paris"));
    }

    [Test]
    public void StreamingChunks_CreatesChunksWithStopOnLast()
    {
        var chunks = FakeResponses.StreamingChunks("Hello", " ", "world");

        Assert.That(chunks, Has.Count.EqualTo(3));
        Assert.That(chunks[0].Content, Is.EqualTo("Hello"));
        Assert.That(chunks[0].FinishReason, Is.Null);
        Assert.That(chunks[1].Content, Is.EqualTo(" "));
        Assert.That(chunks[1].FinishReason, Is.Null);
        Assert.That(chunks[2].Content, Is.EqualTo("world"));
        Assert.That(chunks[2].FinishReason, Is.EqualTo("stop"));
    }

    [Test]
    public void StreamingChunks_SingleChunk()
    {
        var chunks = FakeResponses.StreamingChunks("done");

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0].FinishReason, Is.EqualTo("stop"));
    }

    [Test]
    public void Embedding_CreatesDefaultVector()
    {
        var response = FakeResponses.Embedding();

        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.Embeddings, Has.Count.EqualTo(1));
        Assert.That(response.Embeddings[0], Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
        Assert.That(response.Model, Is.EqualTo("fake-model"));
    }

    [Test]
    public void Embedding_WithCustomVector()
    {
        var response = FakeResponses.Embedding([1.0f, 2.0f]);

        Assert.That(response.Embeddings[0], Is.EqualTo(new[] { 1.0f, 2.0f }));
    }

    [Test]
    public void Embeddings_MultipleVectors()
    {
        var response = FakeResponses.Embeddings([[0.1f], [0.2f], [0.3f]]);

        Assert.That(response.Embeddings, Has.Count.EqualTo(3));
    }

    [Test]
    public void EmbeddingError_CreatesErrorResponse()
    {
        var response = FakeResponses.EmbeddingError("model not found");

        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Is.EqualTo("model not found"));
    }
}
