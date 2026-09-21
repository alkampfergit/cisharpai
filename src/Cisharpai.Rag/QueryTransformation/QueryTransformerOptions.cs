namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Shared options for LLM-based query transformers.
/// </summary>
public sealed class QueryTransformerOptions
{
    /// <summary>
    /// The model to use for the transformation call. When null, the client's default is used.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Temperature for the transformation call. Defaults to 0.0 for deterministic rewrites.
    /// Multi-query and HyDE may benefit from higher values.
    /// </summary>
    public double Temperature { get; set; }
}
