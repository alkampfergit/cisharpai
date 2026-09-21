using Cisharpai.Rag.Pipeline;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.Pipeline;

[TestFixture]
public class RagPipelineBuilderTests
{
    [Test]
    public void Build_NoRetrieverNoChatClient_Throws()
    {
        var builder = new RagPipelineBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void Build_WithRetrieverOnly_Succeeds()
    {
        var retriever = FakeResponses.Retriever();

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .Build();

        Assert.That(pipeline, Is.Not.Null);
    }

    [Test]
    public void Build_WithChatClientOnly_Succeeds()
    {
        var chatClient = new FakeChatCompletionClient();

        var pipeline = new RagPipelineBuilder()
            .WithChatClient(chatClient)
            .Build();

        Assert.That(pipeline, Is.Not.Null);
    }

    [Test]
    public void Build_FullPipeline_Succeeds()
    {
        var pipeline = new RagPipelineBuilder()
            .WithRetriever(FakeResponses.Retriever())
            .WithRetriever(FakeResponses.Retriever())
            .WithQueryTransformer(new FakeQueryTransformer { DefaultResponse = ["q1"] })
            .WithReranker(new FakeRerankerClient())
            .WithContextPacker(NSubstitute.Substitute.For<Cisharpai.Rag.Packing.IContextPacker>())
            .WithChatClient(new FakeChatCompletionClient())
            .WithRankFusionK(30)
            .Build();

        Assert.That(pipeline, Is.Not.Null);
    }

    [Test]
    public void WithRetriever_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RagPipelineBuilder().WithRetriever(null!));
    }

    [Test]
    public void WithChatClient_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RagPipelineBuilder().WithChatClient(null!));
    }

    [Test]
    public void WithRankFusionK_ZeroOrNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RagPipelineBuilder().WithRankFusionK(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RagPipelineBuilder().WithRankFusionK(-1));
    }

    [Test]
    public void WithRankFusionK_Infinity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RagPipelineBuilder().WithRankFusionK(double.PositiveInfinity));
    }
}
