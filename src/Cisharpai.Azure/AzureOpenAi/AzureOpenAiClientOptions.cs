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
    /// Optional explicit underlying model family used for routing decisions
    /// (e.g. whether to use Chat Completions vs the Responses API for GPT-5).
    /// Set this when <see cref="DeploymentName"/> is opaque (e.g. "foo") and does not
    /// carry the model family in its name. Accepts the OpenAI model name prefixes,
    /// e.g. "gpt-5", "o3", "o4", "gpt-4o". When null, routing falls back to the
    /// deployment / request model name; if neither indicates a known family the
    /// standard Chat Completions API is used.
    /// </summary>
    public string? ModelFamily { get; set; }

    /// <summary>
    /// Optional reasoning effort for Azure OpenAI reasoning models when the API version/model supports it.
    /// Values are provider-defined, commonly "low", "medium", or "high".
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>
    /// Optional text verbosity for GPT-5 models via the Responses API.
    /// Values: "low" or "high". Only applied when the model is detected as GPT-5.
    /// </summary>
    public string? TextVerbosity { get; set; }

    /// <summary>
    /// Initializes a new instance with the default API version.
    /// </summary>
    public AzureOpenAiClientOptions()
    {
        ApiVersion = "2024-10-21";
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
