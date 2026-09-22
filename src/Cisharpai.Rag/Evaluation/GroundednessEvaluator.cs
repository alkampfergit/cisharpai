namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Judges whether the answer is supported by the provided context passages.
/// </summary>
public sealed class GroundednessEvaluator : JudgeEvaluatorBase
{
    public GroundednessEvaluator(IChatCompletionClient client) : base(client) { }

    protected override string SystemInstruction =>
        "You are an impartial judge evaluating the groundedness of an answer.\n" +
        "Groundedness measures whether every claim in the answer is supported by the provided context passages.";

    protected override string ScoreInstruction =>
        "Score from 0.0 (completely unsupported) to 1.0 (every claim is grounded in the context).";
}
