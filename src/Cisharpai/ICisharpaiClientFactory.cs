namespace Cisharpai;

public interface ICisharpaiClientFactory
{
    CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        CisharpaiClientConfiguration configuration);

    CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        CisharpaiClientConfiguration configuration);

    CisharpaiClientFactoryResult<IRerankerClient> CreateRerankerClient(
        CisharpaiClientConfiguration configuration) =>
        CisharpaiClientFactoryResult<IRerankerClient>.Failure(
            "Reranker client creation is not supported by this factory implementation.");

    IReadOnlyCollection<CisharpaiProvider> GetRegisteredProviders();
}
