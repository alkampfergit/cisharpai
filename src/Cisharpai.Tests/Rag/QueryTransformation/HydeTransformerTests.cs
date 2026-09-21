using Cisharpai.Rag.QueryTransformation;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.QueryTransformation;

[TestFixture]
public class HydeTransformerTests
{
    [Test]
    public async Task TransformAsync_SingleHypothesis_ReturnsIt()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat(
            "RAG pipelines work by retrieving relevant documents and passing them to an LLM."));

        var hyde = new HydeTransformer(fake);
        var result = await hyde.TransformAsync("How does RAG work?");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Does.Contain("RAG pipelines"));
    }

    [Test]
    public async Task TransformAsync_IncludeOriginal_PrependsThenHypothesis()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("Hypothetical answer text."));

        var hyde = new HydeTransformer(fake, includeOriginal: true);
        var result = await hyde.TransformAsync("What is chunking?");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.EqualTo("What is chunking?"));
        Assert.That(result[1], Is.EqualTo("Hypothetical answer text."));
    }

    [Test]
    public async Task TransformAsync_MultipleHypotheses_ReturnsAll()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("Hypothesis one."));
        fake.EnqueueResponse(FakeResponses.Chat("Hypothesis two."));
        fake.EnqueueResponse(FakeResponses.Chat("Hypothesis three."));

        var hyde = new HydeTransformer(fake, hypothesisCount: 3);
        var result = await hyde.TransformAsync("test");

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task TransformAsync_EmptyHypothesis_FallsBackToOriginal()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("  "));

        var hyde = new HydeTransformer(fake);
        var result = await hyde.TransformAsync("original query");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("original query"));
    }

    [Test]
    public void TransformAsync_ErrorResponse_Throws()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.ChatError("model unavailable"));

        var hyde = new HydeTransformer(fake);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => hyde.TransformAsync("test"));
        Assert.That(ex!.Message, Does.Contain("model unavailable"));
    }

    [Test]
    public void Constructor_NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HydeTransformer(null!));
    }

    [Test]
    public void Constructor_ZeroHypotheses_Throws()
    {
        var fake = new FakeChatCompletionClient();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HydeTransformer(fake, hypothesisCount: 0));
    }

    [Test]
    public async Task TransformAsync_DefaultTemperature_Is0Point7()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("hypothesis"));

        var hyde = new HydeTransformer(fake);
        await hyde.TransformAsync("test");

        Assert.That(fake.ReceivedRequests[0].Temperature, Is.EqualTo(0.7));
    }

    [Test]
    public async Task TransformAsync_CustomPrompt_IsUsed()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("hypothesis"));

        var hyde = new HydeTransformer(fake, systemPrompt: "Write a passage.");
        await hyde.TransformAsync("test");

        Assert.That(fake.ReceivedRequests[0].Messages[0].Content,
            Is.EqualTo("Write a passage."));
    }
}
