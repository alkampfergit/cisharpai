using System.Text;

namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Judges whether the answer is supported by the provided context passages.
/// </summary>
public sealed class GroundednessEvaluator : JudgeEvaluatorBase
{
    public GroundednessEvaluator(IChatCompletionClient client) : base(client) { }

    protected override string BuildPrompt(string question, string answer, IReadOnlyList<string> contexts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an impartial judge evaluating the groundedness of an answer.");
        sb.AppendLine("Groundedness measures whether every claim in the answer is supported by the provided context passages.");
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
        sb.AppendLine("Score from 0.0 (completely unsupported) to 1.0 (every claim is grounded in the context).");
        sb.AppendLine("Respond with JSON: {\"score\": <number>, \"rationale\": \"<brief explanation>\"}");
        return sb.ToString();
    }
}
