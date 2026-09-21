using Cisharpai.Models;
using Cisharpai.Rag.Pipeline;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.Pipeline;

[TestFixture]
public class ConversationQueryRewriterTests
{
    [Test]
    public async Task RewriteAsync_WithHistory_ReturnsRewrittenQuery()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("What is the capital of France?"));

        var rewriter = new ConversationQueryRewriter(fake);
        var history = new List<LlmMessage>
        {
            new(LlmRole.User, "Tell me about France."),
            new(LlmRole.Assistant, "France is a country in Europe.")
        };

        var result = await rewriter.RewriteAsync("What is its capital?", history);

        Assert.That(result, Is.EqualTo("What is the capital of France?"));
    }

    [Test]
    public async Task RewriteAsync_EmptyHistory_ReturnsOriginalQuery()
    {
        var fake = new FakeChatCompletionClient();
        var rewriter = new ConversationQueryRewriter(fake);

        var result = await rewriter.RewriteAsync("standalone query", []);

        Assert.That(result, Is.EqualTo("standalone query"));
        Assert.That(fake.CallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task RewriteAsync_LlmReturnsEmpty_FallsBackToOriginal()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("   "));

        var rewriter = new ConversationQueryRewriter(fake);
        var result = await rewriter.RewriteAsync("original",
            [new LlmMessage(LlmRole.User, "context")]);

        Assert.That(result, Is.EqualTo("original"));
    }

    [Test]
    public async Task RewriteAsync_LlmError_FallsBackToOriginal()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.ChatError("rate limit"));

        var rewriter = new ConversationQueryRewriter(fake);
        var result = await rewriter.RewriteAsync("original",
            [new LlmMessage(LlmRole.User, "context")]);

        Assert.That(result, Is.EqualTo("original"));
    }

    [Test]
    public void RewriteAsync_NullQuery_Throws()
    {
        var rewriter = new ConversationQueryRewriter(new FakeChatCompletionClient());
        Assert.ThrowsAsync<ArgumentNullException>(() =>
            rewriter.RewriteAsync(null!, [new LlmMessage(LlmRole.User, "context")]));
    }

    [Test]
    public void Constructor_NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ConversationQueryRewriter(null!));
    }

    [Test]
    public async Task RewriteAsync_CustomSystemPrompt_IsUsed()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("rewritten"));

        var rewriter = new ConversationQueryRewriter(fake, systemPrompt: "Custom rewrite instructions");
        await rewriter.RewriteAsync("test",
            [new LlmMessage(LlmRole.User, "context")]);

        Assert.That(fake.ReceivedRequests[0].Messages[0].Content,
            Is.EqualTo("Custom rewrite instructions"));
    }

    [Test]
    public async Task RewriteAsync_ModelOption_IsPassedThrough()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("rewritten"));

        var rewriter = new ConversationQueryRewriter(fake, model: "gpt-4o");
        await rewriter.RewriteAsync("test",
            [new LlmMessage(LlmRole.User, "context")]);

        Assert.That(fake.ReceivedRequests[0].Model, Is.EqualTo("gpt-4o"));
    }

    [Test]
    public async Task RewriteAsync_TrimsWhitespace()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("\n  clean query  \n"));

        var rewriter = new ConversationQueryRewriter(fake);
        var result = await rewriter.RewriteAsync("messy query",
            [new LlmMessage(LlmRole.User, "context")]);

        Assert.That(result, Is.EqualTo("clean query"));
    }

    [Test]
    public async Task RewriteAsync_PreservesSystemAndToolRoles()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("rewritten"));

        var rewriter = new ConversationQueryRewriter(fake);
        var history = new List<LlmMessage>
        {
            new(LlmRole.System, "system instruction"),
            new(LlmRole.User, "user question"),
            new(LlmRole.Tool, "tool result"),
            new(LlmRole.Assistant, "assistant answer")
        };

        await rewriter.RewriteAsync("follow up", history);

        var userMsg = fake.ReceivedRequests[0].Messages[1].Content;
        Assert.That(userMsg, Does.Contain("System: system instruction"));
        Assert.That(userMsg, Does.Contain("Tool: tool result"));
        Assert.That(userMsg, Does.Contain("User: user question"));
        Assert.That(userMsg, Does.Contain("Assistant: assistant answer"));
    }
}
