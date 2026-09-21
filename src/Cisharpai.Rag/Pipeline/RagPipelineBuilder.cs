using Cisharpai.Rag.Packing;
using Cisharpai.Rag.QueryTransformation;

namespace Cisharpai.Rag.Pipeline;

/// <summary>
/// Fluent builder for configuring and constructing a <see cref="ConversationalRagPipeline"/>.
/// </summary>
public sealed class RagPipelineBuilder
{
    private readonly List<IRetriever> _retrievers = [];
    private readonly List<IQueryTransformer> _queryTransformers = [];
    private ConversationQueryRewriter? _conversationRewriter;
    private IRerankerClient? _reranker;
    private IContextPacker? _contextPacker;
    private IChatCompletionClient? _chatClient;
    private double _rankFusionK = 60.0;

    public RagPipelineBuilder WithRetriever(IRetriever retriever)
    {
        ArgumentNullException.ThrowIfNull(retriever);
        _retrievers.Add(retriever);
        return this;
    }

    public RagPipelineBuilder WithQueryTransformer(IQueryTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        _queryTransformers.Add(transformer);
        return this;
    }

    public RagPipelineBuilder WithConversationRewriter(ConversationQueryRewriter rewriter)
    {
        ArgumentNullException.ThrowIfNull(rewriter);
        _conversationRewriter = rewriter;
        return this;
    }

    public RagPipelineBuilder WithReranker(IRerankerClient reranker)
    {
        ArgumentNullException.ThrowIfNull(reranker);
        _reranker = reranker;
        return this;
    }

    public RagPipelineBuilder WithContextPacker(IContextPacker packer)
    {
        ArgumentNullException.ThrowIfNull(packer);
        _contextPacker = packer;
        return this;
    }

    public RagPipelineBuilder WithChatClient(IChatCompletionClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        _chatClient = chatClient;
        return this;
    }

    public RagPipelineBuilder WithRankFusionK(double k)
    {
        if (!double.IsFinite(k) || k <= 0)
            throw new ArgumentOutOfRangeException(nameof(k), k, "k must be a finite positive number.");
        _rankFusionK = k;
        return this;
    }

    public IRagPipeline Build()
    {
        if (_retrievers.Count == 0 && _chatClient is null)
            throw new InvalidOperationException(
                "At least one retriever or a chat client must be configured.");

        return new ConversationalRagPipeline(
            _retrievers.ToList(),
            _queryTransformers.ToList(),
            _conversationRewriter,
            _reranker,
            _contextPacker,
            _chatClient,
            _rankFusionK);
    }
}
