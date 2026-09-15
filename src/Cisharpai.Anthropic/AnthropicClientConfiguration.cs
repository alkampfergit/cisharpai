namespace Cisharpai.Anthropic;

public sealed record AnthropicClientConfiguration : CisharpaiClientConfiguration
{
    public override CisharpaiProvider Provider => CisharpaiProvider.Anthropic;

    public string BaseUrl { get; init; } = "https://api.anthropic.com/v1/";

    public string ApiVersion { get; init; } = "2023-06-01";

    public string? DefaultModel { get; init; }

    /// <summary>
    /// The Anthropic web search tool type version string.
    /// Defaults to <c>"web_search_20260209"</c>. Override to pin a specific version
    /// or adopt a newer one without waiting for a library release.
    /// </summary>
    public string WebSearchToolVersion { get; init; } = "web_search_20260209";
}
