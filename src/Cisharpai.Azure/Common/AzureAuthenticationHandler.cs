using System.Net.Http.Headers;
using Azure.Core;

namespace Cisharpai.Azure.Common;

/// <summary>
/// HTTP message handler that adds Azure authentication headers to requests.
/// Supports both API key and Azure AD (TokenCredential) authentication.
/// </summary>
public sealed class AzureAuthenticationHandler : DelegatingHandler
{
    private readonly AzureClientOptionsBase _options;
    private readonly TokenCredential? _credential;
    private static readonly string[] Scopes = ["https://cognitiveservices.azure.com/.default"];

    /// <summary>
    /// Creates a new instance of the authentication handler.
    /// </summary>
    /// <param name="options">Client options containing API key for authentication.</param>
    /// <param name="credential">Optional Azure AD token credential. If provided, takes precedence over API key.</param>
    public AzureAuthenticationHandler(
        AzureClientOptionsBase options,
        TokenCredential? credential = null)
    {
        _options = options;
        _credential = credential;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_credential is not null)
        {
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(Scopes),
                cancellationToken);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Token);
        }
        else
        {
            request.Headers.Add("api-key", _options.ApiKey);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
