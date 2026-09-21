namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Chains multiple <see cref="IQueryTransformer"/> instances sequentially.
/// Each transformer receives every query produced by the previous stage;
/// outputs are flattened and deduplicated.
/// </summary>
public sealed class CompositeQueryTransformer : IQueryTransformer
{
    private readonly IReadOnlyList<IQueryTransformer> _transformers;

    public CompositeQueryTransformer(IEnumerable<IQueryTransformer> transformers)
    {
        ArgumentNullException.ThrowIfNull(transformers);
        _transformers = transformers.ToList();
        if (_transformers.Count == 0)
            throw new ArgumentException("At least one transformer is required.", nameof(transformers));
    }

    public CompositeQueryTransformer(params IQueryTransformer[] transformers)
        : this((IEnumerable<IQueryTransformer>)transformers)
    {
    }

    public async Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        IReadOnlyList<string> current = [query];

        foreach (var transformer in _transformers)
        {
            var next = new List<string>();
            foreach (var q in current)
            {
                var transformed = await transformer.TransformAsync(q, cancellationToken)
                    .ConfigureAwait(false);
                next.AddRange(transformed);
            }

            current = next.Distinct(StringComparer.Ordinal).ToList();
        }

        return current;
    }
}
