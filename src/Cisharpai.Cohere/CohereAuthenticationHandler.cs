using System.Net.Http.Headers;

namespace Cisharpai.Cohere;

public sealed class CohereAuthenticationHandler : DelegatingHandler
{
    private readonly CohereClientOptions _options;

    public CohereAuthenticationHandler(CohereClientOptions options)
    {
        _options = options;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
