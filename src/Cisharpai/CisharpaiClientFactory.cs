namespace Cisharpai;

internal sealed class CisharpaiClientFactory : ICisharpaiClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<CisharpaiProvider, IClientFactoryProvider> _providers;

    public CisharpaiClientFactory(
        IServiceProvider serviceProvider,
        IEnumerable<IClientFactoryProvider> providers)
    {
        _serviceProvider = serviceProvider;
        _providers = new Dictionary<CisharpaiProvider, IClientFactoryProvider>();
        foreach (var provider in providers)
        {
            _providers[provider.Provider] = provider;
        }
    }

    public CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        CisharpaiClientConfiguration configuration)
    {
        if (!_providers.TryGetValue(configuration.Provider, out var provider))
        {
            var registered = string.Join(", ", _providers.Keys);
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Failure(
                $"Provider '{configuration.Provider}' is not registered. Registered providers: {registered}.");
        }

        if (!provider.SupportsChatCompletion)
        {
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Failure(
                $"Provider '{configuration.Provider}' does not support chat completion clients.");
        }

        return provider.CreateChatCompletionClient(_serviceProvider, configuration);
    }

    public CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        CisharpaiClientConfiguration configuration)
    {
        if (!_providers.TryGetValue(configuration.Provider, out var provider))
        {
            var registered = string.Join(", ", _providers.Keys);
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Failure(
                $"Provider '{configuration.Provider}' is not registered. Registered providers: {registered}.");
        }

        if (!provider.SupportsEmbedding)
        {
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Failure(
                $"Provider '{configuration.Provider}' does not support embedding clients.");
        }

        return provider.CreateEmbeddingClient(_serviceProvider, configuration);
    }

    public IReadOnlyCollection<CisharpaiProvider> GetRegisteredProviders() =>
        _providers.Keys.ToArray();
}
