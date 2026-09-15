namespace Cisharpai.Models;

/// <summary>
/// Options for provider-hosted web search.
/// The model decides whether to search; the search executes server-side within a single request.
/// <para>
/// <strong>Cost warning:</strong> enabling web search incurs a per-search charge on top of token
/// costs (e.g. ~$10 per 1,000 searches on Anthropic). Monitor
/// <see cref="ChatCompletionResponse.WebSearchCount"/> to track billable searches.
/// </para>
/// </summary>
public sealed record WebSearchOptions
{
    /// <summary>
    /// When true, the provider's server-side web search tool is included in the request.
    /// The model may or may not invoke it depending on the query.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
