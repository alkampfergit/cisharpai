namespace Cisharpai.Anthropic;

public sealed class AnthropicClientOptions
{
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string? ApiVersion { get; set; }
}
