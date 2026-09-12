using System.Collections.Concurrent;

namespace Cisharpai.Testing;

public sealed class FakeClientFactoryProvider : IClientFactoryProvider
{
    private readonly ConcurrentQueue<IChatCompletionClient> _chatClients = new();
    private readonly ConcurrentQueue<IEmbeddingClient> _embeddingClients = new();
    private readonly ConcurrentQueue<IRerankerClient> _rerankerClients = new();

    private IChatCompletionClient? _defaultChatClient;
    private IEmbeddingClient? _defaultEmbeddingClient;
    private IRerankerClient? _defaultRerankerClient;

    public CisharpaiProvider Provider { get; }

    public bool SupportsChatCompletion => true;

    public bool SupportsEmbedding => true;

    public bool SupportsReranking => true;

    public FakeClientFactoryProvider(CisharpaiProvider provider = CisharpaiProvider.OpenAi)
    {
        Provider = provider;
    }

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

    public FakeClientFactoryProvider EnqueueRerankerClient(IRerankerClient client)
    {
        _rerankerClients.Enqueue(client);
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

    public FakeClientFactoryProvider WithDefaultRerankerClient(IRerankerClient client)
    {
        _defaultRerankerClient = client;
        return this;
    }

    public CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (_chatClients.TryDequeue(out var client))
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(client);

        if (_defaultChatClient is not null)
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(_defaultChatClient);

        return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(new FakeChatCompletionClient());
    }

    public CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (_embeddingClients.TryDequeue(out var client))
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(client);

        if (_defaultEmbeddingClient is not null)
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(_defaultEmbeddingClient);

        return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(new FakeEmbeddingClient());
    }

    public CisharpaiClientFactoryResult<IRerankerClient> CreateRerankerClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (_rerankerClients.TryDequeue(out var client))
            return CisharpaiClientFactoryResult<IRerankerClient>.Success(client);

        if (_defaultRerankerClient is not null)
            return CisharpaiClientFactoryResult<IRerankerClient>.Success(_defaultRerankerClient);

        return CisharpaiClientFactoryResult<IRerankerClient>.Success(new FakeRerankerClient());
    }
}
