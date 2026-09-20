using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Cisharpai.Rag;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers RAG ingestion using the application's unkeyed embedding client.</summary>
    public static IServiceCollection AddCisharpaiRag(
        this IServiceCollection services, Action<RagOptions>? configure = null) =>
        services.AddCisharpaiRag(sp => sp.GetRequiredService<IEmbeddingClient>(), configure);

    /// <summary>Registers RAG ingestion with a provider factory, including keyed/scoped clients.</summary>
    /// <exception cref="InvalidOperationException">Thrown when RAG services have already been registered.</exception>
    public static IServiceCollection AddCisharpaiRag(
        this IServiceCollection services,
        Func<IServiceProvider, IEmbeddingClient> embeddingClientFactory,
        Action<RagOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(embeddingClientFactory);
        if (services.Any(d => d.ServiceType == typeof(IRagIngestionPipeline)))
            throw new InvalidOperationException("AddCisharpaiRag has already been called. Only a single RAG registration is supported.");

        services.AddOptions<RagOptions>();
        if (configure is not null)
            services.Configure(configure);

        services.TryAddSingleton<ITextChunker>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            ArgumentNullException.ThrowIfNull(options.Chunking);
            return new FixedSizeChunker(options.Chunking);
        });
        services.TryAddScoped<IBulkEmbeddingProcessor>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            ArgumentNullException.ThrowIfNull(options.Embedding);
            return new BulkEmbeddingProcessor(embeddingClientFactory(sp), options.Embedding);
        });
        services.TryAddScoped<IRagIngestionPipeline, RagIngestionPipeline>();
        return services;
    }
}
