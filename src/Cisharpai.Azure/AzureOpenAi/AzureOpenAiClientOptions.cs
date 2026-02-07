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

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("ApiKey is required.");
        }
    }
}
