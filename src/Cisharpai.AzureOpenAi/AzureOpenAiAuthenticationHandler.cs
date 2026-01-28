using System.Net.Http.Headers;
using Azure.Core;

namespace Cisharpai.AzureOpenAi;

public sealed class AzureOpenAiAuthenticationHandler : DelegatingHandler
{
    private readonly AzureOpenAiClientOptions _options;
    private readonly TokenCredential? _credential;

    public AzureOpenAiAuthenticationHandler(
        AzureOpenAiClientOptions options,
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
                new TokenRequestContext(
                    new[] { "https://cognitiveservices.azure.com/.default" }),
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
