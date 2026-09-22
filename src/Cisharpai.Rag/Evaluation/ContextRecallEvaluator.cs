namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Judges whether the retrieved context covers the information needed to answer the question.
/// </summary>
public sealed class ContextRecallEvaluator : JudgeEvaluatorBase
{
    public ContextRecallEvaluator(IChatCompletionClient client) : base(client) { }

    protected override string SystemInstruction =>
        "You are an impartial judge evaluating context recall for a RAG system.\n" +
        "Context recall measures whether the retrieved passages contain all the information needed to fully answer the question.\n" +
        "Compare the answer (treated as ground truth) against the contexts to determine what fraction of the answer's claims can be found in the contexts.";

    protected override string ContextHeader => "Retrieved context passages";

    protected override string AnswerHeader => "Answer (ground truth)";

    protected override string ScoreInstruction =>
        "Score from 0.0 (contexts miss all needed information) to 1.0 (contexts cover everything needed for the answer).";
}
