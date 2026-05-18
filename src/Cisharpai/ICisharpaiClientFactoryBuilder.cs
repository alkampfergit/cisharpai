using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai;

public interface ICisharpaiClientFactoryBuilder
{
    ICisharpaiClientFactoryBuilder AddProvider(IClientFactoryProvider provider);

    IServiceCollection Services { get; }
}
