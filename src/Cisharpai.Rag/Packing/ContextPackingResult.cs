namespace Cisharpai.Rag.Packing;

/// <summary>
/// The result of packing ranked chunks into a token budget.
/// </summary>
/// <param name="Selected">Chunks included in the packed context, in final order (rank or lost-in-the-middle).</param>
/// <param name="Dropped">Chunks excluded from the packed context, with reasons.</param>
/// <param name="TotalTokensUsed">Total tokens consumed by the selected chunks and separators.</param>
/// <param name="BudgetRemaining">Tokens left in the budget after packing.</param>
public sealed record ContextPackingResult(
    IReadOnlyList<ScoredChunk> Selected,
    IReadOnlyList<DroppedChunk> Dropped,
    int TotalTokensUsed,
    int BudgetRemaining);
