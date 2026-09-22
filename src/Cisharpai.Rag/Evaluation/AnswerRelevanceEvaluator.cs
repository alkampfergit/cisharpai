namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Judges whether the answer addresses the question asked.
/// </summary>
public sealed class AnswerRelevanceEvaluator : JudgeEvaluatorBase
{
    public AnswerRelevanceEvaluator(IChatCompletionClient client) : base(client) { }

    protected override string SystemInstruction =>
        "You are an impartial judge evaluating the relevance of an answer to a question.\n" +
        "Answer relevance measures whether the answer directly addresses the question asked.";

    protected override string ScoreInstruction =>
        "Score from 0.0 (completely irrelevant) to 1.0 (directly and fully addresses the question).";
}
