using System.Net.Http.Headers;

namespace cisharpai.OpenAi;

public sealed class OpenAiAuthenticationHandler : DelegatingHandler
{
    private readonly OpenAiClientOptions _options;

    public OpenAiAuthenticationHandler(OpenAiClientOptions options)
    {
        _options = options;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        if (!string.IsNullOrWhiteSpace(_options.Organization))
            request.Headers.Add("OpenAI-Organization", _options.Organization);

        return base.SendAsync(request, cancellationToken);
    }
}
