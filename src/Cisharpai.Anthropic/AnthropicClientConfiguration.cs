namespace Cisharpai.Anthropic;

public sealed record AnthropicClientConfiguration : CisharpaiClientConfiguration
{
    public override CisharpaiProvider Provider => CisharpaiProvider.Anthropic;

    public string BaseUrl { get; init; } = "https://api.anthropic.com/v1/";

    public string ApiVersion { get; init; } = "2023-06-01";

    public string? DefaultModel { get; init; }
}
