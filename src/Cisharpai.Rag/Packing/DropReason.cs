namespace Cisharpai.Rag.Packing;

/// <summary>
/// Why a chunk was excluded from the packed context.
/// </summary>
public enum DropReason
{
    /// <summary>The remaining budget could not accommodate the chunk.</summary>
    BudgetExhausted,

    /// <summary>The chunk alone exceeds the entire available budget (after reserved tokens).</summary>
    IndividuallyOversized
}
