using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai;

public static class CisharpaiClientFactoryExtensions
{
    public static ICisharpaiClientFactoryBuilder AddCisharpaiClientFactory(
        this IServiceCollection services)
    {
        services.AddSingleton<ICisharpaiClientFactory>(sp =>
            new CisharpaiClientFactory(sp, sp.GetServices<IClientFactoryProvider>()));

        return new CisharpaiClientFactoryBuilder(services);
    }

    private sealed class CisharpaiClientFactoryBuilder : ICisharpaiClientFactoryBuilder
    {
        public IServiceCollection Services { get; }

        public CisharpaiClientFactoryBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public ICisharpaiClientFactoryBuilder AddProvider(IClientFactoryProvider provider)
        {
            Services.AddSingleton(provider);
            return this;
        }
    }
}
