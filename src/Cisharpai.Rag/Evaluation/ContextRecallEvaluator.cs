using System.Text;

namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Judges whether the retrieved context covers the information needed to answer the question.
/// </summary>
public sealed class ContextRecallEvaluator : JudgeEvaluatorBase
{
    public ContextRecallEvaluator(IChatCompletionClient client) : base(client) { }

    protected override string BuildPrompt(string question, string answer, IReadOnlyList<string> contexts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an impartial judge evaluating context recall for a RAG system.");
        sb.AppendLine("Context recall measures whether the retrieved passages contain all the information needed to fully answer the question.");
        sb.AppendLine("Compare the answer (treated as ground truth) against the contexts to determine what fraction of the answer's claims can be found in the contexts.");
        sb.AppendLine();
        sb.AppendLine("## Retrieved context passages");
        for (var i = 0; i < contexts.Count; i++)
            sb.AppendLine($"[{i + 1}] {contexts[i]}");
        sb.AppendLine();
        sb.AppendLine("## Question");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine("## Answer (ground truth)");
        sb.AppendLine(answer);
        sb.AppendLine();
        sb.AppendLine("Score from 0.0 (contexts miss all needed information) to 1.0 (contexts cover everything needed for the answer).");
        sb.AppendLine("Respond with JSON: {\"score\": <number>, \"rationale\": \"<brief explanation>\"}");
        return sb.ToString();
    }
}
