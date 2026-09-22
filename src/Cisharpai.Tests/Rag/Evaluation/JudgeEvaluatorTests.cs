using Cisharpai.Models;
using Cisharpai.Rag.Evaluation;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.Evaluation;

[TestFixture]
public class JudgeEvaluatorTests
{
    private static ChatCompletionResponse MakeJsonResponse(double score, string rationale) =>
        new(
            Content: $"{{\"score\": {score}, \"rationale\": \"{rationale}\"}}",
            Model: "test-model",
            PromptTokens: 10,
            CompletionTokens: 10);

    private static FakeChatCompletionClient CreateClientWithScore(double score, string rationale = "test")
    {
        var client = new FakeChatCompletionClient();
        client.EnqueueJsonOutputResponse(MakeJsonResponse(score, rationale));
        return client;
    }

    // --- Groundedness ---

    [Test]
    public async Task GroundednessEvaluator_ReturnsScore()
    {
        var client = CreateClientWithScore(0.85, "Most claims are supported");
        var evaluator = new GroundednessEvaluator(client);

        var result = await evaluator.EvaluateAsync(
            "What is X?", "X is Y", new[] { "X is Y according to source" });

        Assert.That(result.Score, Is.EqualTo(0.85).Within(1e-10));
        Assert.That(result.Rationale, Is.EqualTo("Most claims are supported"));
    }

    [Test]
    public async Task GroundednessEvaluator_CapturesRequest()
    {
        var client = CreateClientWithScore(0.5);
        var evaluator = new GroundednessEvaluator(client);

        await evaluator.EvaluateAsync("q", "a", new[] { "ctx1", "ctx2" });

        Assert.That(client.ReceivedJsonOutputRequests, Has.Count.EqualTo(1));
        var (request, options) = client.ReceivedJsonOutputRequests[0];
        Assert.That(request.Messages, Has.Count.EqualTo(2));
        Assert.That(request.Messages[0].Role, Is.EqualTo(LlmRole.System));
        Assert.That(request.Messages[0].Content, Does.Contain("groundedness"));
        Assert.That(request.Messages[0].Content, Does.Contain("UNTRUSTED DATA"));
        Assert.That(request.Messages[1].Role, Is.EqualTo(LlmRole.User));
        Assert.That(request.Messages[1].Content, Does.Contain("[1] ctx1"));
        Assert.That(request.Messages[1].Content, Does.Contain("[2] ctx2"));
        Assert.That(options.Mode, Is.EqualTo(JsonOutputMode.JsonSchema));
    }

    // --- Answer Relevance ---

    [Test]
    public async Task AnswerRelevanceEvaluator_ReturnsScore()
    {
        var client = CreateClientWithScore(0.9, "Directly answers the question");
        var evaluator = new AnswerRelevanceEvaluator(client);

        var result = await evaluator.EvaluateAsync(
            "What is X?", "X is Y", new[] { "context" });

        Assert.That(result.Score, Is.EqualTo(0.9).Within(1e-10));
        Assert.That(result.Rationale, Is.EqualTo("Directly answers the question"));
    }

    [Test]
    public async Task AnswerRelevanceEvaluator_PromptMentionsRelevance()
    {
        var client = CreateClientWithScore(0.5);
        var evaluator = new AnswerRelevanceEvaluator(client);

        await evaluator.EvaluateAsync("q", "a", new[] { "ctx" });

        var systemContent = client.ReceivedJsonOutputRequests[0].Request.Messages[0].Content;
        Assert.That(systemContent, Does.Contain("relevance"));
    }

    // --- Context Precision ---

    [Test]
    public async Task ContextPrecisionEvaluator_ReturnsScore()
    {
        var client = CreateClientWithScore(0.75, "3 of 4 passages relevant");
        var evaluator = new ContextPrecisionEvaluator(client);

        var result = await evaluator.EvaluateAsync(
            "What is X?", "X is Y", new[] { "relevant1", "irrelevant", "relevant2", "relevant3" });

        Assert.That(result.Score, Is.EqualTo(0.75).Within(1e-10));
    }

    [Test]
    public async Task ContextPrecisionEvaluator_PromptMentionsPrecision()
    {
        var client = CreateClientWithScore(0.5);
        var evaluator = new ContextPrecisionEvaluator(client);

        await evaluator.EvaluateAsync("q", "a", new[] { "ctx" });

        var systemContent = client.ReceivedJsonOutputRequests[0].Request.Messages[0].Content;
        Assert.That(systemContent, Does.Contain("precision"));
    }

    // --- Context Recall ---

    [Test]
    public async Task ContextRecallEvaluator_ReturnsScore()
    {
        var client = CreateClientWithScore(0.6, "Some information missing from context");
        var evaluator = new ContextRecallEvaluator(client);

        var result = await evaluator.EvaluateAsync(
            "What is X?", "X is Y and Z", new[] { "X is Y" });

        Assert.That(result.Score, Is.EqualTo(0.6).Within(1e-10));
    }

    [Test]
    public async Task ContextRecallEvaluator_PromptMentionsRecall()
    {
        var client = CreateClientWithScore(0.5);
        var evaluator = new ContextRecallEvaluator(client);

        await evaluator.EvaluateAsync("q", "a", new[] { "ctx" });

        var systemContent = client.ReceivedJsonOutputRequests[0].Request.Messages[0].Content;
        Assert.That(systemContent, Does.Contain("recall"));
    }

    // --- Shared behavior ---

    [Test]
    public void AllEvaluators_RequireJsonOutputFeature()
    {
        var client = new FakeChatCompletionClient(FakeChatFeatures.Streaming);

        Assert.That(() => new GroundednessEvaluator(client), Throws.InvalidOperationException);
        Assert.That(() => new AnswerRelevanceEvaluator(client), Throws.InvalidOperationException);
        Assert.That(() => new ContextPrecisionEvaluator(client), Throws.InvalidOperationException);
        Assert.That(() => new ContextRecallEvaluator(client), Throws.InvalidOperationException);
    }

    [Test]
    public void AllEvaluators_NullClient_Throws()
    {
        Assert.That(() => new GroundednessEvaluator(null!), Throws.ArgumentNullException);
        Assert.That(() => new AnswerRelevanceEvaluator(null!), Throws.ArgumentNullException);
        Assert.That(() => new ContextPrecisionEvaluator(null!), Throws.ArgumentNullException);
        Assert.That(() => new ContextRecallEvaluator(null!), Throws.ArgumentNullException);
    }

    [Test]
    public async Task Evaluator_UsesTemperatureZero()
    {
        var client = CreateClientWithScore(0.5);
        var evaluator = new GroundednessEvaluator(client);

        await evaluator.EvaluateAsync("q", "a", new[] { "ctx" });

        Assert.That(client.ReceivedJsonOutputRequests[0].Request.Temperature, Is.EqualTo(0.0));
    }

    [Test]
    public void Evaluator_LlmError_Throws()
    {
        var client = new FakeChatCompletionClient();
        client.EnqueueJsonOutputResponse(ChatCompletionResponse.Error("rate limit"));
        var evaluator = new GroundednessEvaluator(client);

        Assert.That(
            async () => await evaluator.EvaluateAsync("q", "a", new[] { "ctx" }),
            Throws.InvalidOperationException.With.Message.Contains("rate limit"));
    }

    [Test]
    public async Task Evaluator_ScoreClamped_WhenModelReturnsOutOfRange()
    {
        var client = new FakeChatCompletionClient();
        client.EnqueueJsonOutputResponse(new ChatCompletionResponse(
            Content: "{\"score\": 1.5, \"rationale\": \"overconfident\"}",
            Model: "test", PromptTokens: 1, CompletionTokens: 1));
        var evaluator = new GroundednessEvaluator(client);

        var result = await evaluator.EvaluateAsync("q", "a", new[] { "ctx" });
        Assert.That(result.Score, Is.EqualTo(1.0));
    }

    [Test]
    public async Task Evaluator_ScoreClamped_WhenModelReturnsNegative()
    {
        var client = new FakeChatCompletionClient();
        client.EnqueueJsonOutputResponse(new ChatCompletionResponse(
            Content: "{\"score\": -0.3, \"rationale\": \"underconfident\"}",
            Model: "test", PromptTokens: 1, CompletionTokens: 1));
        var evaluator = new GroundednessEvaluator(client);

        var result = await evaluator.EvaluateAsync("q", "a", new[] { "ctx" });
        Assert.That(result.Score, Is.EqualTo(0.0));
    }

    [Test]
    public async Task Evaluator_EmptyContexts_StillWorks()
    {
        var client = CreateClientWithScore(0.0, "No context provided");
        var evaluator = new GroundednessEvaluator(client);

        var result = await evaluator.EvaluateAsync("q", "a", Array.Empty<string>());
        Assert.That(result.Score, Is.EqualTo(0.0));
    }

    [Test]
    public void Evaluator_NullQuestion_Throws()
    {
        var client = CreateClientWithScore(0.5);
        var evaluator = new GroundednessEvaluator(client);

        Assert.That(
            async () => await evaluator.EvaluateAsync(null!, "a", new[] { "ctx" }),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Evaluator_NullAnswer_Throws()
    {
        var client = CreateClientWithScore(0.5);
        var evaluator = new GroundednessEvaluator(client);

        Assert.That(
            async () => await evaluator.EvaluateAsync("q", null!, new[] { "ctx" }),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Evaluator_NullContexts_Throws()
    {
        var client = CreateClientWithScore(0.5);
        var evaluator = new GroundednessEvaluator(client);

        Assert.That(
            async () => await evaluator.EvaluateAsync("q", "a", null!),
            Throws.ArgumentNullException);
    }
}
