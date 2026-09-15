namespace Cisharpai.Anthropic;

public sealed class AnthropicClientOptions
{
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1/";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiVersion { get; set; } = "2023-06-01";

    public string? DefaultModel { get; set; }

    /// <summary>
    /// The Anthropic web search tool type version string.
    /// Defaults to <c>"web_search_20260209"</c>. Override to pin a specific version
    /// or adopt a newer one without waiting for a library release.
    /// </summary>
    public string WebSearchToolVersion { get; set; } = "web_search_20260209";
}
