namespace Cisharpai;

public interface IClientFactoryProvider
{
    CisharpaiProvider Provider { get; }

    bool SupportsChatCompletion { get; }

    bool SupportsEmbedding { get; }

    CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration);

    CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration);
}
