namespace Cisharpai.Tests.Common;

/// <summary>
/// Constants for environment variable names used in integration tests.
/// </summary>
public static class TestEnvironmentVariables
{
    // OpenAI
    public const string OpenAiTestApiKey = "OPENAI_TEST_API_KEY";

    // Anthropic
    public const string AnthropicTestApiKey = "ANTHROPIC_TEST_API_KEY";

    // Azure OpenAI
    public const string AzureOpenAiTestEndpoint = "AZURE_OPENAI_TEST_ENDPOINT";
    public const string AzureOpenAiTestApiKey = "AZURE_OPENAI_TEST_API_KEY";
    public const string AzureOpenAiTestDeployments = "AZURE_OPENAI_TEST_DEPLOYMENTS";
    public const string AzureOpenAiTestEmbeddingDeployment = "AZURE_OPENAI_TEST_EMBEDDING_DEPLOYMENT";

    // Azure AI Inference
    public const string AzureAiInferenceTestEndpoint = "AZURE_INFERENCE_TEST_ENDPOINT";
    public const string AzureAiInferenceTestApiKey = "AZURE_INFERENCE_TEST_API_KEY";
    public const string AzureAiInferenceTestModels = "AZURE_INFERENCE_TEST_MODELS";
    public const string AzureAiInferenceTestEmbeddingEndpoint = "AZURE_INFERENCE_TEST_EMBEDDING_ENDPOINT";
    public const string AzureAiInferenceTestEmbeddingKey = "AZURE_INFERENCE_TEST_EMBEDDING_KEY";
    public const string AzureAiInferenceTestEmbeddingModel = "AZURE_INFERENCE_TEST_EMBEDDING_MODEL";

    // Cohere
    public const string CohereTestApiKey = "COHERE_TEST_API_KEY";
}
