using Cisharpai.Tests.Common;

namespace Cisharpai.Integration.Tests;

/// <summary>
/// Helper class for loading environment variables in integration tests.
/// Delegates to <see cref="DotEnvLoader"/> for the actual loading logic.
/// </summary>
public static class DotEnv
{
    // Re-export constants for backwards compatibility
    public const string OpenAiTestApiKey = TestEnvironmentVariables.OpenAiTestApiKey;
    public const string AnthropicTestApiKey = TestEnvironmentVariables.AnthropicTestApiKey;
    public const string AzureOpenAiTestEndpoint = TestEnvironmentVariables.AzureOpenAiTestEndpoint;
    public const string AzureOpenAiTestApiKey = TestEnvironmentVariables.AzureOpenAiTestApiKey;
    public const string AzureOpenAiTestDeployments = TestEnvironmentVariables.AzureOpenAiTestDeployments;
    public const string AzureOpenAiTestEmbeddingDeployment = TestEnvironmentVariables.AzureOpenAiTestEmbeddingDeployment;
    public const string CohereTestApiKey = TestEnvironmentVariables.CohereTestApiKey;
    public const string AzureAiInferenceTestEndpoint = TestEnvironmentVariables.AzureAiInferenceTestEndpoint;
    public const string AzureAiInferenceTestApiKey = TestEnvironmentVariables.AzureAiInferenceTestApiKey;
    public const string AzureAiInferenceTestModels = TestEnvironmentVariables.AzureAiInferenceTestModels;
    public const string AzureAiInferenceTestEmbeddingEndpoint = TestEnvironmentVariables.AzureAiInferenceTestEmbeddingEndpoint;
    public const string AzureAiInferenceTestEmbeddingKey = TestEnvironmentVariables.AzureAiInferenceTestEmbeddingKey;
    public const string AzureAiInferenceTestEmbeddingModel = TestEnvironmentVariables.AzureAiInferenceTestEmbeddingModel;

    /// <summary>
    /// Loads environment variables from a .env file found in the current
    /// directory or any parent directory.
    /// </summary>
    public static void Load() => DotEnvLoader.Load();
}
