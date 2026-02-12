namespace Cisharpai.Integration.Tests;

/// <summary>
/// Validates environment configuration for all providers.
/// Run this test first to ensure your .env file is properly configured.
/// </summary>
public sealed class EnvironmentConfigurationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    [Test]
    [Category("Configuration")]
    public void AllProviders_EnvironmentVariables_AreConfigured()
    {
        var allVars = new (string Name, string Provider, string Example)[]
        {
            (DotEnv.OpenAiTestApiKey, "OpenAI", "sk-your-openai-api-key-here"),
            (DotEnv.AnthropicTestApiKey, "Anthropic", "sk-ant-your-anthropic-api-key-here"),
            (DotEnv.AzureOpenAiTestEndpoint, "Azure OpenAI", "https://your-resource-name.openai.azure.com/"),
            (DotEnv.AzureOpenAiTestApiKey, "Azure OpenAI", "your-azure-openai-api-key-here"),
            (DotEnv.AzureOpenAiTestDeployments, "Azure OpenAI", "gpt-4o,gpt-4o-mini"),
            (DotEnv.AzureOpenAiTestEmbeddingDeployment, "Azure OpenAI", "text-embedding-ada-002"),
            (DotEnv.AzureAiInferenceTestEndpoint, "Azure AI Inference", "https://your-model-endpoint.region.inference.ai.azure.com/"),
            (DotEnv.AzureAiInferenceTestApiKey, "Azure AI Inference", "your-azure-inference-api-key-here"),
            (DotEnv.AzureAiInferenceTestModels, "Azure AI Inference", "Phi-3-mini-4k-instruct,Mistral-large"),
            (DotEnv.AzureAiInferenceTestEmbeddingEndpoint, "Azure AI Inference", "https://your-embedding-endpoint.region.inference.ai.azure.com"),
            (DotEnv.AzureAiInferenceTestEmbeddingKey, "Azure AI Inference", "your-azure-inference-embedding-key-here"),
            (DotEnv.AzureAiInferenceTestEmbeddingModel, "Azure AI Inference", "Cohere-embed-v3-english"),
            (DotEnv.CohereTestApiKey, "Cohere", "your-cohere-api-key-here"),
        };

        var missing = allVars
            .Where(v => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v.Name)))
            .ToList();

        if (missing.Count == 0)
            return;

        var missingByProvider = missing
            .GroupBy(v => v.Provider)
            .OrderBy(g => g.Key)
            .Select(g => $"  • {g.Key}: {string.Join(", ", g.Select(v => v.Name))}")
            .ToList();

        var envFileContent = string.Join(Environment.NewLine,
            missing.Select(v => $"{v.Name}={v.Example}"));

        Assert.Fail($"""

══════════════════════════════════════════════════════════════════
Integration Tests - Missing Environment Variables
══════════════════════════════════════════════════════════════════

The following environment variables are not set:

{string.Join(Environment.NewLine, missingByProvider)}

To fix this, add the following to your .env file in the repository root:

──────────────────────────────────────────────────────────────────
{envFileContent}
──────────────────────────────────────────────────────────────────

NOTE: You only need to configure the providers you want to test.
Tests for unconfigured providers will fail with clear messages.

══════════════════════════════════════════════════════════════════
""");
    }
}
