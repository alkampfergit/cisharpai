namespace Cisharpai;

public interface IClientFactoryProvider
{
    CisharpaiProvider Provider { get; }

    bool SupportsChatCompletion { get; }

    bool SupportsEmbedding { get; }

    /// <summary>
    /// Whether this provider can create reranker clients. Defaults to <c>false</c> so existing
    /// providers keep compiling without change.
    /// </summary>
    bool SupportsReranking => false;

    CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration);

    CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration);

    CisharpaiClientFactoryResult<IRerankerClient> CreateRerankerClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration) =>
        CisharpaiClientFactoryResult<IRerankerClient>.Failure(
            $"Provider '{Provider}' does not support reranker clients.");
}
