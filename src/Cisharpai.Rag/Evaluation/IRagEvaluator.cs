namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// LLM-as-judge scorer that evaluates a RAG interaction on a single dimension.
/// </summary>
public interface IRagEvaluator
{
    Task<EvaluationScore> EvaluateAsync(
        string question,
        string answer,
        IReadOnlyList<string> contexts,
        CancellationToken cancellationToken = default);
}
