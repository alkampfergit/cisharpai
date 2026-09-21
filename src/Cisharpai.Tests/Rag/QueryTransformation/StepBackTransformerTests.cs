using Cisharpai.Rag.QueryTransformation;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.QueryTransformation;

[TestFixture]
public class StepBackTransformerTests
{
    [Test]
    public async Task TransformAsync_IncludeOriginal_ReturnsBothQueries()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat(
            "What are the key principles of information retrieval?"));

        var stepBack = new StepBackTransformer(fake);
        var result = await stepBack.TransformAsync(
            "Why does BM25 underperform on short queries?");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.EqualTo("Why does BM25 underperform on short queries?"));
        Assert.That(result[1], Does.Contain("information retrieval"));
    }

    [Test]
    public async Task TransformAsync_ExcludeOriginal_ReturnsOnlyStepBack()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("broader question"));

        var stepBack = new StepBackTransformer(fake, includeOriginal: false);
        var result = await stepBack.TransformAsync("specific question");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("broader question"));
    }

    [Test]
    public async Task TransformAsync_EmptyStepBack_FallsBackToOriginal()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("  "));

        var stepBack = new StepBackTransformer(fake, includeOriginal: false);
        var result = await stepBack.TransformAsync("original");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("original"));
    }

    [Test]
    public void TransformAsync_ErrorResponse_Throws()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.ChatError("server error"));

        var stepBack = new StepBackTransformer(fake);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => stepBack.TransformAsync("test"));
        Assert.That(ex!.Message, Does.Contain("server error"));
    }

    [Test]
    public void Constructor_NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new StepBackTransformer(null!));
    }

    [Test]
    public async Task TransformAsync_DefaultTemperature_IsZero()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("step back"));

        var stepBack = new StepBackTransformer(fake);
        await stepBack.TransformAsync("test");

        Assert.That(fake.ReceivedRequests[0].Temperature, Is.EqualTo(0.0));
    }

    [Test]
    public async Task TransformAsync_CustomPrompt_IsUsed()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("step back"));

        var stepBack = new StepBackTransformer(fake, systemPrompt: "Be more abstract.");
        await stepBack.TransformAsync("test");

        Assert.That(fake.ReceivedRequests[0].Messages[0].Content,
            Is.EqualTo("Be more abstract."));
    }

    [Test]
    public async Task TransformAsync_OptionsModel_IsPassedThrough()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("step back"));

        var stepBack = new StepBackTransformer(fake,
            options: new QueryTransformerOptions { Model = "claude-sonnet-5" });
        await stepBack.TransformAsync("test");

        Assert.That(fake.ReceivedRequests[0].Model, Is.EqualTo("claude-sonnet-5"));
    }
}
