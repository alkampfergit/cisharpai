using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Testing;

/// <summary>
/// DI registration helpers for fake clients in test scenarios.
/// </summary>
public static class FakeServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="FakeChatCompletionClient"/> as <see cref="IChatCompletionClient"/>.
    /// Returns the fake instance for setup and assertions.
    /// </summary>
    public static FakeChatCompletionClient AddFakeChatCompletionClient(
        this IServiceCollection services,
        FakeChatFeatures enabledFeatures = FakeChatFeatures.All)
    {
        var fake = new FakeChatCompletionClient(enabledFeatures);
        services.AddSingleton<IChatCompletionClient>(fake);
        return fake;
    }

    /// <summary>
    /// Registers a <see cref="FakeEmbeddingClient"/> as <see cref="IEmbeddingClient"/>.
    /// Returns the fake instance for setup and assertions.
    /// </summary>
    public static FakeEmbeddingClient AddFakeEmbeddingClient(
        this IServiceCollection services,
        FakeEmbeddingFeatures enabledFeatures = FakeEmbeddingFeatures.All)
    {
        var fake = new FakeEmbeddingClient(enabledFeatures);
        services.AddSingleton<IEmbeddingClient>(fake);
        return fake;
    }
}
