namespace Cisharpai.Azure.Common;

/// <summary>
/// Base configuration options shared by all Azure AI clients.
/// Provides common properties for endpoint, authentication, and API version.
/// </summary>
public abstract class AzureClientOptionsBase
{
    /// <summary>
    /// The Azure service endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key for authentication. Required if not using Azure AD token credential.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// API version for the Azure service.
    /// </summary>
    public string ApiVersion { get; set; } = string.Empty;

    /// <summary>
    /// Validates the base options configuration.
    /// Derived classes should call base.Validate() and add their own validation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("Endpoint is required.");
        }

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new InvalidOperationException("Endpoint must be a valid HTTP or HTTPS URL.");
        }
    }
}
