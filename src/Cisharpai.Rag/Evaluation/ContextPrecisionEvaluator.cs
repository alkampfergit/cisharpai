using System.Text;

namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Judges whether the retrieved context chunks are relevant to the question.
/// </summary>
public sealed class ContextPrecisionEvaluator : JudgeEvaluatorBase
{
    public ContextPrecisionEvaluator(IChatCompletionClient client) : base(client) { }

    protected override string BuildPrompt(string question, string answer, IReadOnlyList<string> contexts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an impartial judge evaluating context precision for a RAG system.");
        sb.AppendLine("Context precision measures what fraction of the retrieved passages are actually relevant to answering the question.");
        sb.AppendLine();
        sb.AppendLine("## Retrieved context passages");
        for (var i = 0; i < contexts.Count; i++)
            sb.AppendLine($"[{i + 1}] {contexts[i]}");
        sb.AppendLine();
        sb.AppendLine("## Question");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine("## Answer");
        sb.AppendLine(answer);
        sb.AppendLine();
        sb.AppendLine("Score from 0.0 (no passage is relevant) to 1.0 (every passage is relevant to the question).");
        sb.AppendLine("Respond with JSON: {\"score\": <number>, \"rationale\": \"<brief explanation>\"}");
        return sb.ToString();
    }
}
