namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Judges whether the retrieved context chunks are relevant to the question.
/// </summary>
public sealed class ContextPrecisionEvaluator : JudgeEvaluatorBase
{
    public ContextPrecisionEvaluator(IChatCompletionClient client) : base(client) { }

    protected override string SystemInstruction =>
        "You are an impartial judge evaluating context precision for a RAG system.\n" +
        "Context precision measures what fraction of the retrieved passages are actually relevant to answering the question.";

    protected override string ContextHeader => "Retrieved context passages";

    protected override string ScoreInstruction =>
        "Score from 0.0 (no passage is relevant) to 1.0 (every passage is relevant to the question).";
}
