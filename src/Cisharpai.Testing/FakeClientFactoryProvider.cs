namespace Cisharpai.Testing;

public sealed class FakeClientFactoryProvider : IClientFactoryProvider
{
    private readonly Queue<IChatCompletionClient> _chatClients = new();
    private readonly Queue<IEmbeddingClient> _embeddingClients = new();

    private IChatCompletionClient? _defaultChatClient;
    private IEmbeddingClient? _defaultEmbeddingClient;

    public CisharpaiProvider Provider => CisharpaiProvider.OpenAi;

    public bool SupportsChatCompletion => true;

    public bool SupportsEmbedding => true;

    public FakeClientFactoryProvider EnqueueChatClient(IChatCompletionClient client)
    {
        _chatClients.Enqueue(client);
        return this;
    }

    public FakeClientFactoryProvider EnqueueEmbeddingClient(IEmbeddingClient client)
    {
        _embeddingClients.Enqueue(client);
        return this;
    }

    public FakeClientFactoryProvider WithDefaultChatClient(IChatCompletionClient client)
    {
        _defaultChatClient = client;
        return this;
    }

    public FakeClientFactoryProvider WithDefaultEmbeddingClient(IEmbeddingClient client)
    {
        _defaultEmbeddingClient = client;
        return this;
    }

    public CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (_chatClients.Count > 0)
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(_chatClients.Dequeue());

        if (_defaultChatClient is not null)
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(_defaultChatClient);

        return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(new FakeChatCompletionClient());
    }

    public CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (_embeddingClients.Count > 0)
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(_embeddingClients.Dequeue());

        if (_defaultEmbeddingClient is not null)
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(_defaultEmbeddingClient);

        return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(new FakeEmbeddingClient());
    }
}
