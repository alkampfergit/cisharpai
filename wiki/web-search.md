# Web Search

`IWebSearchFeature` enables provider-hosted server-side web search. The model decides whether to search, executes the search within a single request (no client-side round trip), and returns cited answers.

## Supported Providers

| Provider | Support | Notes |
|----------|---------|-------|
| Anthropic | Yes | `web_search` server-side tool, configurable version |
| OpenAI | GPT-5 only | Responses API `web_search` tool |
| Azure OpenAI | -- | Not yet supported |
| Azure AI Inference | -- | Not supported |
| Cohere | -- | Not supported |

## Usage

```csharp
if (client.Features.Get<IWebSearchFeature>() is { } webSearch)
{
    var request = new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "What happened in tech news today?")],
        Model: "claude-sonnet-4-5");

    var response = await webSearch.GetChatCompletionWithWebSearchAsync(
        request,
        new WebSearchOptions());

    if (response.ChatCompletion.IsSuccess)
    {
        Console.WriteLine(response.ChatCompletion.Content);

        // Citations reference the web pages used
        foreach (var citation in response.Citations)
        {
            var url = citation.Source.Id;                 // page URL
            var title = citation.Source.Data["title"];     // page title
            var citedText = citation.Source.CitedText;     // quoted excerpt
            Console.WriteLine($"  [{title}]({url})");
        }

        // Billable search count
        Console.WriteLine($"Searches: {response.ChatCompletion.WebSearchCount}");
    }
}
```

## WebSearchOptions

```csharp
public record WebSearchOptions(bool Enabled = true);
```

The `Enabled` property defaults to `true`. The options object exists for future extensibility (e.g., search region, result count limits).

## Response Structure

`GetChatCompletionWithWebSearchAsync` returns a `GroundedChatCompletionResponse` — the same type used by `IGroundedChatFeature`. This means:

- `response.ChatCompletion` — the standard `ChatCompletionResponse` with `Content`, `IsSuccess`, `ErrorMessage`, etc.
- `response.Citations` — a list of `Citation` objects referencing the web pages used.
- `response.GroundingKind` — set to `GroundingKind.WebSearch` to distinguish web-search citations from document-grounded citations.

### Citation Mapping

Citations from both providers follow the same unified structure:

| Field | Value |
|-------|-------|
| `CitationSource.Id` | Page URL |
| `CitationSource.Data["title"]` | Page title |
| `CitationSource.CitedText` | Quoted text excerpt (Anthropic only) |
| `Citation.StartCharOffset` / `EndCharOffset` | Position in the response content |

### WebSearchCount

`ChatCompletionResponse.WebSearchCount` reports the number of billable web searches performed:

- **Anthropic**: from `usage.server_tool_use.web_search_requests`
- **OpenAI**: count of `web_search_call` output items

## Provider Details

### Anthropic

Anthropic's `web_search` tool is injected as a server-side tool in the request. The tool version is configurable via `AnthropicClientOptions.WebSearchToolVersion` (default: `web_search_20260209`).

```csharp
services.AddAnthropicClient(o =>
{
    o.ApiKey = "sk-ant-...";
    o.WebSearchToolVersion = "web_search_20260209"; // default
});
```

Anthropic citations use `web_search_result_location` type with `url`, `title`, and `cited_text` fields.

### OpenAI

OpenAI web search uses the Responses API `web_search` tool and is available for GPT-5 models only. Non-GPT-5 models return `IsSuccess = false` with a descriptive error.

OpenAI citations use `url_citation` annotations with `url`, `title`, `start_index`, and `end_index` fields.

## Cost Warning

Each web search incurs a per-search charge on top of token costs. On Anthropic this is approximately $10 per 1,000 searches. Monitor `WebSearchCount` to track usage.

## Testing

Use `FakeChatCompletionClient` with `FakeResponses.WebSearch()`:

```csharp
var fake = new FakeChatCompletionClient();
fake.EnqueueWebSearchResponse(FakeResponses.WebSearch("Paris is the capital of France."));

var response = await fake.GetChatCompletionWithWebSearchAsync(request, new WebSearchOptions());
// response.GroundingKind == GroundingKind.WebSearch
// response.ChatCompletion.WebSearchCount == 1
```

See [Testing with Cisharpai](testing.md) for the full queue/default/capture pattern.
