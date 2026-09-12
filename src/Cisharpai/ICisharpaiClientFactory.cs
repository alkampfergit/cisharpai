namespace Cisharpai;

public interface ICisharpaiClientFactory
{
    CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        CisharpaiClientConfiguration configuration);

    CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        CisharpaiClientConfiguration configuration);

    CisharpaiClientFactoryResult<IRerankerClient> CreateRerankerClient(
        CisharpaiClientConfiguration configuration);

    IReadOnlyCollection<CisharpaiProvider> GetRegisteredProviders();
}
