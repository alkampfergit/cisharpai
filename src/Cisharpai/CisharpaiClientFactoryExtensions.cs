using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cisharpai;

public static class CisharpaiClientFactoryExtensions
{
    public static ICisharpaiClientFactoryBuilder AddCisharpaiClientFactory(
        this IServiceCollection services)
    {
        services.TryAddSingleton<ICisharpaiClientFactory>(sp =>
            new CisharpaiClientFactory(sp, sp.GetServices<IClientFactoryProvider>()));

        return new CisharpaiClientFactoryBuilder(services);
    }

    private sealed class CisharpaiClientFactoryBuilder : ICisharpaiClientFactoryBuilder
    {
        private readonly HashSet<CisharpaiProvider> _registeredProviders = new();

        public IServiceCollection Services { get; }

        public CisharpaiClientFactoryBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public ICisharpaiClientFactoryBuilder AddProvider(IClientFactoryProvider provider)
        {
            if (!_registeredProviders.Add(provider.Provider))
                return this;

            Services.AddSingleton(provider);
            return this;
        }
    }
}
