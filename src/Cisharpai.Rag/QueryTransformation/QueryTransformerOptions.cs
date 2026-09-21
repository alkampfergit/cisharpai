namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Shared options for LLM-based query transformers.
/// </summary>
public sealed class QueryTransformerOptions
{
    /// <summary>
    /// The model to use for the transformation call. When null, the client's default is used.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Temperature override for the transformation call.
    /// When null, each transformer uses its own default (0.0 for deterministic rewrites,
    /// 0.7 for multi-query and HyDE).
    /// </summary>
    public double? Temperature { get; init; }

    internal QueryTransformerOptions Snapshot() => new() { Model = Model, Temperature = Temperature };
}
