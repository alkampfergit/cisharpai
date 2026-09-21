using Cisharpai.Rag.QueryTransformation;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.QueryTransformation;

[TestFixture]
public class QueryRewriterTests
{
    [Test]
    public async Task TransformAsync_ReturnsRewrittenQuery()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("What are the benefits of RAG pipelines?"));

        var rewriter = new QueryRewriter(fake);
        var result = await rewriter.TransformAsync("hey so like what are the benefits of rag pipelines?");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("What are the benefits of RAG pipelines?"));
    }

    [Test]
    public async Task TransformAsync_EmptyResponseContent_FallsBackToOriginal()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("   "));

        var rewriter = new QueryRewriter(fake);
        var result = await rewriter.TransformAsync("my query");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("my query"));
    }

    [Test]
    public void TransformAsync_ErrorResponse_Throws()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.ChatError("rate limit"));

        var rewriter = new QueryRewriter(fake);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => rewriter.TransformAsync("test"));
        Assert.That(ex!.Message, Does.Contain("rate limit"));
    }

    [Test]
    public void TransformAsync_NullQuery_Throws()
    {
        var fake = new FakeChatCompletionClient();
        var rewriter = new QueryRewriter(fake);

        Assert.ThrowsAsync<ArgumentNullException>(() => rewriter.TransformAsync(null!));
    }

    [Test]
    public void TransformAsync_WhitespaceQuery_Throws()
    {
        var fake = new FakeChatCompletionClient();
        var rewriter = new QueryRewriter(fake);

        Assert.ThrowsAsync<ArgumentException>(() => rewriter.TransformAsync("  "));
    }

    [Test]
    public void Constructor_NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new QueryRewriter(null!));
    }

    [Test]
    public async Task TransformAsync_CustomSystemPrompt_IsUsed()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("rewritten"));

        var rewriter = new QueryRewriter(fake, systemPrompt: "Custom instructions");
        await rewriter.TransformAsync("test");

        Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
        Assert.That(fake.ReceivedRequests[0].Messages[0].Content,
            Is.EqualTo("Custom instructions"));
    }

    [Test]
    public async Task TransformAsync_OptionsModel_IsPassedThrough()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("rewritten"));

        var rewriter = new QueryRewriter(fake,
            options: new QueryTransformerOptions { Model = "gpt-4o" });
        await rewriter.TransformAsync("test");

        Assert.That(fake.ReceivedRequests[0].Model, Is.EqualTo("gpt-4o"));
    }

    [Test]
    public async Task TransformAsync_TrimsWhitespace()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("\n  clean query  \n"));

        var rewriter = new QueryRewriter(fake);
        var result = await rewriter.TransformAsync("messy query");

        Assert.That(result[0], Is.EqualTo("clean query"));
    }
}
