namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Transforms a user query into one or more queries that improve retrieval quality.
/// Implementations are composable (chain them) and optional in the pipeline.
/// </summary>
public interface IQueryTransformer
{
    /// <summary>
    /// Transforms a single query into one or more output queries.
    /// A single-output transformer (e.g. <see cref="QueryRewriter"/>) returns exactly one item.
    /// A multi-output transformer (e.g. <see cref="MultiQueryExpander"/>) returns N variants.
    /// </summary>
    Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default);
}
