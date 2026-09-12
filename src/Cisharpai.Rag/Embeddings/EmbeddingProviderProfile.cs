namespace Cisharpai.Rag.Embeddings;

/// <summary>
/// Named batch-sizing presets for the providers shipped with Cisharpai.
/// Values are conservative starting points chosen to stay inside each provider's documented
/// per-request ceilings; they are not authoritative provider limits. Verify them against your
/// deployment and model, and override <see cref="BulkEmbeddingOptions.MaxBatchItems"/> or
/// <see cref="BulkEmbeddingOptions.MaxBatchTokens"/> when your deployment differs.
/// </summary>
public enum EmbeddingProviderProfile
{
    /// <summary>Provider-agnostic fallback: 32 items per request, no token budget.</summary>
    Conservative = 0,

    /// <summary>OpenAI embeddings: 96 items, 250,000 estimated tokens per request.</summary>
    OpenAi = 1,

    /// <summary>Azure OpenAI embeddings: 16 items, 100,000 estimated tokens per request.</summary>
    AzureOpenAi = 2,

    /// <summary>Azure AI Inference embeddings: 64 items, 100,000 estimated tokens per request.</summary>
    AzureAiInference = 3,

    /// <summary>Cohere embeddings: 96 items, 100,000 estimated tokens per request.</summary>
    Cohere = 4
}
