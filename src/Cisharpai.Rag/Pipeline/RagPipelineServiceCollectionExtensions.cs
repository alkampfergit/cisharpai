using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cisharpai.Rag.Pipeline;

public static class RagPipelineServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="IRagPipeline"/> built via the <paramref name="configure"/> callback.
    /// </summary>
    public static IServiceCollection AddCisharpaiRagPipeline(
        this IServiceCollection services,
        Action<RagPipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.TryAddSingleton<IRagPipeline>(sp =>
        {
            var builder = new RagPipelineBuilder();
            configure(builder);
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Registers a scoped <see cref="IRagPipeline"/> built from services resolved at request time.
    /// </summary>
    public static IServiceCollection AddCisharpaiRagPipeline(
        this IServiceCollection services,
        Action<IServiceProvider, RagPipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.TryAddScoped<IRagPipeline>(sp =>
        {
            var builder = new RagPipelineBuilder();
            configure(sp, builder);
            return builder.Build();
        });

        return services;
    }
}
