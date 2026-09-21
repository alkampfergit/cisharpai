using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Models;

namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Base class for LLM-as-judge evaluators that use <see cref="IJsonOutputFeature"/>
/// to get structured scores from any <see cref="IChatCompletionClient"/>.
/// </summary>
public abstract class JudgeEvaluatorBase : IRagEvaluator
{
    private static readonly string OutputSchema = JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            score = new { type = "number", description = "A score between 0.0 and 1.0" },
            rationale = new { type = "string", description = "Brief explanation for the score" }
        },
        required = new[] { "score", "rationale" },
        additionalProperties = false
    });

    private readonly IJsonOutputFeature _jsonOutput;

    protected JudgeEvaluatorBase(IChatCompletionClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _jsonOutput = client.Features.Get<IJsonOutputFeature>()
            ?? throw new InvalidOperationException(
                $"The provided {nameof(IChatCompletionClient)} does not support {nameof(IJsonOutputFeature)}. " +
                "Use a client that registers IJsonOutputFeature (all built-in providers do).");
    }

    protected abstract string BuildPrompt(string question, string answer, IReadOnlyList<string> contexts);

    public async Task<EvaluationScore> EvaluateAsync(
        string question,
        string answer,
        IReadOnlyList<string> contexts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(contexts);

        var prompt = BuildPrompt(question, answer, contexts);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, prompt)],
            Temperature: 0.0);

        var jsonOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "evaluation_score",
            SchemaDescription: "A normalized evaluation score with rationale",
            JsonSchema: OutputSchema,
            Strict: true);

        var response = await _jsonOutput.GetChatCompletionWithJsonOutputAsync(
            request, jsonOptions, cancellationToken);

        if (!response.IsSuccess)
            throw new InvalidOperationException(
                $"Evaluation LLM call failed: {response.ErrorMessage}");

        return ParseScore(response.Content);
    }

    private static EvaluationScore ParseScore(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var score = root.GetProperty("score").GetDouble();
        var rationale = root.GetProperty("rationale").GetString() ?? string.Empty;

        score = Math.Clamp(score, 0.0, 1.0);

        return new EvaluationScore(score, rationale);
    }
}
