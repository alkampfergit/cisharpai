using Cisharpai.Azure.Common;

namespace Cisharpai.Azure.AzureOpenAi;

/// <summary>
/// Configuration options for Azure OpenAI clients.
/// </summary>
public sealed class AzureOpenAiClientOptions : AzureClientOptionsBase
{
    /// <summary>
    /// The deployment name for the Azure OpenAI model.
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Optional default model name used for model-type detection (e.g. reasoning vs legacy).
    /// When <see cref="ChatCompletionRequest.Model"/> is null, this value is used as fallback.
    /// </summary>
    public string? DefaultModel { get; set; }

    /// <summary>
    /// Initializes a new instance with the default API version.
    /// </summary>
    public AzureOpenAiClientOptions()
    {
        ApiVersion = "2024-02-01";
    }

    /// <summary>
    /// Validates the Azure OpenAI configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(DeploymentName))
        {
            throw new InvalidOperationException("DeploymentName is required.");
        }

    }

    /// <summary>
    /// Validates that either an API key or a token credential is configured for authentication.
    /// Called by DI extensions where both options and credential are available.
    /// </summary>
    /// <param name="hasTokenCredential">Whether a TokenCredential was provided.</param>
    /// <exception cref="InvalidOperationException">Thrown when neither authentication method is configured.</exception>
    internal void ValidateAuthentication(bool hasTokenCredential)
    {
        if (!hasTokenCredential && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Either ApiKey or a TokenCredential is required for authentication.");
        }
    }
}
