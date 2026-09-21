using Cisharpai.Rag.QueryTransformation;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.QueryTransformation;

[TestFixture]
public class CompositeQueryTransformerTests
{
    [Test]
    public async Task TransformAsync_ChainsTransformersSequentially()
    {
        var rewriterClient = new FakeChatCompletionClient();
        rewriterClient.DefaultResponse = FakeResponses.Chat("clean query");

        var expanderClient = new FakeChatCompletionClient();
        expanderClient.DefaultResponse = FakeResponses.Chat("variant A\nvariant B");

        var rewriter = new QueryRewriter(rewriterClient);
        var expander = new MultiQueryExpander(expanderClient, variantCount: 2);

        var composite = new CompositeQueryTransformer(rewriter, expander);
        var result = await composite.TransformAsync("messy query?");

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("clean query"));
        Assert.That(result[1], Is.EqualTo("variant A"));
        Assert.That(result[2], Is.EqualTo("variant B"));
    }

    [Test]
    public async Task TransformAsync_DeduplicatesOutputs()
    {
        var client1 = new FakeChatCompletionClient();
        client1.DefaultResponse = FakeResponses.Chat("same query");

        var client2 = new FakeChatCompletionClient();
        client2.DefaultResponse = FakeResponses.Chat("same query");

        var t1 = new QueryRewriter(client1);
        var t2 = new QueryRewriter(client2);

        var composite = new CompositeQueryTransformer(t1, t2);
        var result = await composite.TransformAsync("original");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("same query"));
    }

    [Test]
    public async Task TransformAsync_FanOut_MultiQueryThenStepBack()
    {
        var expanderClient = new FakeChatCompletionClient();
        expanderClient.DefaultResponse = FakeResponses.Chat("variant");

        var stepBackClient = new FakeChatCompletionClient();
        stepBackClient.DefaultResponse = FakeResponses.Chat("broader");

        var expander = new MultiQueryExpander(expanderClient, variantCount: 1);
        var stepBack = new StepBackTransformer(stepBackClient);

        var composite = new CompositeQueryTransformer(expander, stepBack);
        var result = await composite.TransformAsync("specific question");

        Assert.That(result, Has.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void Constructor_NullElement_Throws()
    {
        var client = new FakeChatCompletionClient();
        client.DefaultResponse = FakeResponses.Chat("x");
        var rewriter = new QueryRewriter(client);

        Assert.Throws<ArgumentException>(
            () => new CompositeQueryTransformer(rewriter, null!));
    }

    [Test]
    public void Constructor_EmptyList_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new CompositeQueryTransformer(Array.Empty<IQueryTransformer>()));
    }

    [Test]
    public void Constructor_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CompositeQueryTransformer((IEnumerable<IQueryTransformer>)null!));
    }

    [Test]
    public void TransformAsync_NullQuery_Throws()
    {
        var client = new FakeChatCompletionClient();
        client.DefaultResponse = FakeResponses.Chat("x");
        var composite = new CompositeQueryTransformer(new QueryRewriter(client));

        Assert.ThrowsAsync<ArgumentNullException>(() => composite.TransformAsync(null!));
    }
}
