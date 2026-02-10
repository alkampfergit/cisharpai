namespace Cisharpai.Anthropic;

public sealed class AnthropicAuthenticationHandler : DelegatingHandler
{
    private readonly AnthropicClientOptions _options;

    public AnthropicAuthenticationHandler(AnthropicClientOptions options)
    {
        _options = options;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", _options.ApiVersion);

        return base.SendAsync(request, cancellationToken);
    }
}
