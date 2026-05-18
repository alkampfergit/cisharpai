namespace Cisharpai;

public interface ICisharpaiClientFactory
{
    CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        CisharpaiClientConfiguration configuration);

    CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        CisharpaiClientConfiguration configuration);

    IReadOnlyCollection<CisharpaiProvider> GetRegisteredProviders();
}
