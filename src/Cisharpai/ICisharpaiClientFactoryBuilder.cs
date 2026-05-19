using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai;

public interface ICisharpaiClientFactoryBuilder
{
    ICisharpaiClientFactoryBuilder AddProvider(IClientFactoryProvider provider);

    bool IsProviderRegistered(CisharpaiProvider provider);

    IServiceCollection Services { get; }
}
