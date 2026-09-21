using System.Text;

namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Judges whether the answer addresses the question asked.
/// </summary>
public sealed class AnswerRelevanceEvaluator : JudgeEvaluatorBase
{
    public AnswerRelevanceEvaluator(IChatCompletionClient client) : base(client) { }

    protected override string BuildPrompt(string question, string answer, IReadOnlyList<string> contexts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an impartial judge evaluating the relevance of an answer to a question.");
        sb.AppendLine("Answer relevance measures whether the answer directly addresses the question asked.");
        sb.AppendLine();
        sb.AppendLine("## Context passages");
        for (var i = 0; i < contexts.Count; i++)
            sb.AppendLine($"[{i + 1}] {contexts[i]}");
        sb.AppendLine();
        sb.AppendLine("## Question");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine("## Answer");
        sb.AppendLine(answer);
        sb.AppendLine();
        sb.AppendLine("Score from 0.0 (completely irrelevant) to 1.0 (directly and fully addresses the question).");
        sb.AppendLine("Respond with JSON: {\"score\": <number>, \"rationale\": \"<brief explanation>\"}");
        return sb.ToString();
    }
}
