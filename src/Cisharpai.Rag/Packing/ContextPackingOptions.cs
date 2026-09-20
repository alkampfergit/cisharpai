namespace Cisharpai.Rag.Packing;

/// <summary>
/// Per-call settings for context packing. Validated and snapshotted at the start of each
/// <see cref="IContextPacker.PackAsync"/> call.
/// </summary>
public sealed class ContextPackingOptions
{
    public int TokenBudget { get; set; }
    public int ReservedTokens { get; set; }
    public string Separator { get; set; } = "\n\n";
    public bool UseLostInMiddleOrdering { get; set; } = true;
    public OverflowStrategy OverflowStrategy { get; set; } = OverflowStrategy.SkipAndContinue;

    internal ContextPackingOptions Snapshot() => ValidateAndClone(
        TokenBudget, ReservedTokens, Separator, UseLostInMiddleOrdering, OverflowStrategy);

    private static ContextPackingOptions ValidateAndClone(
        int tokenBudget, int reservedTokens, string separator,
        bool useLostInMiddleOrdering, OverflowStrategy overflowStrategy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokenBudget);
        ArgumentOutOfRangeException.ThrowIfNegative(reservedTokens);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(reservedTokens, tokenBudget);
        ArgumentNullException.ThrowIfNull(separator);
        if (!Enum.IsDefined(overflowStrategy))
            throw new ArgumentOutOfRangeException(nameof(overflowStrategy), overflowStrategy, "Unknown overflow strategy.");

        return new ContextPackingOptions
        {
            TokenBudget = tokenBudget,
            ReservedTokens = reservedTokens,
            Separator = separator,
            UseLostInMiddleOrdering = useLostInMiddleOrdering,
            OverflowStrategy = overflowStrategy
        };
    }
}
