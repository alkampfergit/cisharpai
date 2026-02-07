using Cisharpai.Azure.Common;

namespace Cisharpai.Azure.AzureAiInference;

/// <summary>
/// Configuration options for Azure AI Inference clients.
/// Supports both API key and Azure AD (TokenCredential) authentication.
/// </summary>
public sealed class AzureAiInferenceClientOptions : AzureClientOptionsBase
{
    /// <summary>
    /// Model identifier. For multi-model endpoints, this specifies which model to use.
    /// Examples: "Phi-3-mini-4k-instruct", "Llama-3-8B-Instruct", "Mistral-7B-Instruct"
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance with the default API version.
    /// </summary>
    public AzureAiInferenceClientOptions()
    {
        ApiVersion = "2024-05-01-preview";
    }

    /// <summary>
    /// Validates the Azure AI Inference configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(ModelId))
        {
            throw new InvalidOperationException("ModelId is required.");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("ApiKey is required.");
        }
    }
}
