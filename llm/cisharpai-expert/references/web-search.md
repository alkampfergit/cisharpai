# Web Search (`IWebSearchFeature`)

Provider-hosted server-side web search. The model decides whether to search, executes the search within a single request, and returns cited answers.

## Providers

| Provider | Support | Details |
|----------|---------|---------|
| Anthropic | Yes | `web_search` server-side tool; version configurable via `AnthropicClientOptions.WebSearchToolVersion` (default `web_search_20260209`) |
| OpenAI | GPT-5 only | Responses API `web_search` tool; non-GPT-5 returns `IsSuccess=false` |

## Interface

```csharp
public interface IWebSearchFeature
{
    Task<GroundedChatCompletionResponse> GetChatCompletionWithWebSearchAsync(
        ChatCompletionRequest request,
        WebSearchOptions options,
        CancellationToken cancellationToken = default);
}
```

## WebSearchOptions

```csharp
public sealed record WebSearchOptions
{
    public bool Enabled { get; init; } = true;
}
```

## Usage

```csharp
if (client.Features.Get<IWebSearchFeature>() is { } webSearch)
{
    var request = new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "What happened in tech news today?")],
        Model: "claude-sonnet-4-5");

    var response = await webSearch.GetChatCompletionWithWebSearchAsync(
        request, new WebSearchOptions());

    if (response.ChatCompletion.IsSuccess)
    {
        Console.WriteLine(response.ChatCompletion.Content);

        foreach (var citation in response.Citations)
        {
            var source = citation.Sources[0];
            var url = source.Id;
            var title = source.Data?["title"];
            Console.WriteLine($"  [{title}]({url})");
        }

        Console.WriteLine($"Searches: {response.ChatCompletion.WebSearchCount}");
    }
}
```

## Response

Returns `GroundedChatCompletionResponse` (same type as `IGroundedChatFeature`):

- `ChatCompletion` — standard response with `Content`, `IsSuccess`, `WebSearchCount`
- `Citations` — web page references with `Sources[0].Id` = URL, `Sources[0].Data?["title"]` = page title
- `GroundingKind` — `GroundingKind.WebSearch`

### WebSearchCount

`ChatCompletionResponse.WebSearchCount` (`int? init` property) reports billable searches:
- Anthropic: `usage.server_tool_use.web_search_requests`
- OpenAI: count of `web_search_call` output items

## Anthropic Configuration

```csharp
services.AddAnthropicClient(o =>
{
    o.ApiKey = "sk-ant-...";
    o.WebSearchToolVersion = "web_search_20260209"; // default
});
```

## Cost

Per-search charge on top of token costs (~$10/1k searches on Anthropic). Monitor `WebSearchCount`.

## Testing

```csharp
var fake = new FakeChatCompletionClient();
fake.EnqueueWebSearchResponse(FakeResponses.WebSearch("answer"));

var response = await fake.GetChatCompletionWithWebSearchAsync(request, new WebSearchOptions());
// response.GroundingKind == GroundingKind.WebSearch
// response.ChatCompletion.WebSearchCount == 1
```

Queue, default, and capture patterns work identically to other features.
See [testing.md](testing.md) for the full pattern.
