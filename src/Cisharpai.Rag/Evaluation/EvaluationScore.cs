namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Normalized [0, 1] score returned by an LLM-as-judge evaluator.
/// </summary>
public sealed record EvaluationScore(double Score, string Rationale)
{
    public double Score { get; } = !double.IsFinite(Score) || Score < 0.0 || Score > 1.0
        ? throw new ArgumentOutOfRangeException(nameof(Score), Score, "Score must be in [0, 1].")
        : Score;

    public string Rationale { get; } = Rationale ?? throw new ArgumentNullException(nameof(Rationale));
}
