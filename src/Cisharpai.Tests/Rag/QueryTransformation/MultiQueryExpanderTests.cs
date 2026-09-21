using Cisharpai.Rag.QueryTransformation;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.QueryTransformation;

[TestFixture]
public class MultiQueryExpanderTests
{
    [Test]
    public async Task TransformAsync_ReturnsOriginalPlusVariants()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat(
            "How does RAG improve accuracy?\nWhat retrieval methods work best for QA?\nWhy use vector search with LLMs?"));

        var expander = new MultiQueryExpander(fake, variantCount: 3);
        var result = await expander.TransformAsync("How does RAG work?");

        Assert.That(result, Has.Count.EqualTo(4));
        Assert.That(result[0], Is.EqualTo("How does RAG work?"));
        Assert.That(result[1], Is.EqualTo("How does RAG improve accuracy?"));
    }

    [Test]
    public async Task TransformAsync_ExcludeOriginal_OmitsIt()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("variant one\nvariant two"));

        var expander = new MultiQueryExpander(fake, variantCount: 2, includeOriginal: false);
        var result = await expander.TransformAsync("original");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.EqualTo("variant one"));
    }

    [Test]
    public void TransformAsync_ErrorResponse_Throws()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.ChatError("timeout"));

        var expander = new MultiQueryExpander(fake);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => expander.TransformAsync("test"));
        Assert.That(ex!.Message, Does.Contain("timeout"));
    }

    [Test]
    public void Constructor_ZeroVariants_Throws()
    {
        var fake = new FakeChatCompletionClient();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MultiQueryExpander(fake, variantCount: 0));
    }

    [Test]
    public void Constructor_NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MultiQueryExpander(null!));
    }

    [Test]
    public async Task TransformAsync_BlankLinesIgnored()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("line one\n\n\nline two\n  \nline three"));

        var expander = new MultiQueryExpander(fake, variantCount: 3);
        var result = await expander.TransformAsync("original");

        Assert.That(result, Has.Count.EqualTo(4));
        Assert.That(result[1], Is.EqualTo("line one"));
        Assert.That(result[2], Is.EqualTo("line two"));
        Assert.That(result[3], Is.EqualTo("line three"));
    }

    [Test]
    public async Task TransformAsync_CustomSystemPrompt_IsUsed()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("v1"));

        var expander = new MultiQueryExpander(fake, systemPrompt: "Custom multi-query instructions");
        await expander.TransformAsync("test");

        Assert.That(fake.ReceivedRequests[0].Messages[0].Content,
            Is.EqualTo("Custom multi-query instructions"));
    }

    [Test]
    public async Task TransformAsync_DefaultTemperature_Is0Point7()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("v1"));

        var expander = new MultiQueryExpander(fake);
        await expander.TransformAsync("test");

        Assert.That(fake.ReceivedRequests[0].Temperature, Is.EqualTo(0.7));
    }

    [Test]
    public async Task TransformAsync_CapsVariantsToRequestedCount()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("v1\nv2\nv3\nv4\nv5"));

        var expander = new MultiQueryExpander(fake, variantCount: 2);
        var result = await expander.TransformAsync("original");

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("original"));
        Assert.That(result[1], Is.EqualTo("v1"));
        Assert.That(result[2], Is.EqualTo("v2"));
    }

    [Test]
    public async Task TransformAsync_OptionsSnapshotted_MutationDoesNotAffect()
    {
        var fake = new FakeChatCompletionClient();
        fake.DefaultResponse = FakeResponses.Chat("v1");

        var options = new QueryTransformerOptions { Temperature = 0.5 };
        var expander = new MultiQueryExpander(fake, options: options);

        await expander.TransformAsync("test");
        Assert.That(fake.ReceivedRequests[0].Temperature, Is.EqualTo(0.5));
    }

    [Test]
    public async Task TransformAsync_NullTemperatureInOptions_Uses0Point7Default()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("v1"));

        var options = new QueryTransformerOptions { Model = "test-model" };
        var expander = new MultiQueryExpander(fake, options: options);
        await expander.TransformAsync("test");

        Assert.That(fake.ReceivedRequests[0].Temperature, Is.EqualTo(0.7));
    }
}
