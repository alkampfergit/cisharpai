# Contract: IChatCompletionClient

**File**: `src/Cisharpai/IChatCompletionClient.cs`

## Interface Definition

```csharp
public interface IChatCompletionClient : IHasFeatures
{
    Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
```

## Behavioral Contract

### Input

- `request.Messages` — Must contain at least one message. Messages are ordered as a conversation history.
- `request.Model` — When null, the provider's `DefaultModel` is used. When both are null, `InvalidOperationException` is thrown.
- `request.Temperature` — Optional. Provider-specific range. Null means provider default.
- `request.MaxTokens` — Optional. Anthropic defaults to 8192. Others use provider default.
- `request.IncludeRawResponse` — When true, response includes `RawResponseJson` and `RawRequestJson`.
- `request.ExtraParameters` — When non-null and a JSON object, deep-merged into the serialized request body.
- `request.ReasoningEffort` — For reasoning models only. Values: "low", "medium", "high".

### Output (Success)

- `Content` — Non-empty text response from the model.
- `Model` — Actual model name used (may differ from requested).
- `PromptTokens` / `CompletionTokens` — Token usage counts >= 0.
- `IsSuccess` — `true`.
- `ErrorMessage` — `null`.
- `Status` — Provider-specific completion status (e.g., "stop", "end_turn").

### Output (API Error)

- `Content` — Empty string.
- `Model` — Empty string.
- `PromptTokens` / `CompletionTokens` — 0.
- `IsSuccess` — `false`.
- `ErrorMessage` — HTTP status code + response body.
- Created via `ChatCompletionResponse.Error(message, rawBody?)`.

### Output (Refusal)

- `Content` — Empty string.
- `Refusal` — Non-null string explaining the refusal reason.
- `IsSuccess` — `true` (the API call succeeded; the model chose not to answer).

### Exception Behavior

- Network errors (`HttpRequestException`, `TaskCanceledException`) propagate as exceptions.
- Configuration errors (`InvalidOperationException` for missing model) propagate as exceptions.
- API errors (4xx, 5xx) are caught and returned as `ChatCompletionResponse.Error(...)`.

### Threading / Async

- All methods are async and support `CancellationToken`.
- Provider clients are thread-safe (backed by `HttpClient`).
- `FeatureCollection` is thread-safe (`ConcurrentDictionary`-backed).

## Provider Implementations

| Provider | Class | Features Registered |
|----------|-------|-------------------|
| OpenAI | `OpenAiChatCompletionClient` | IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature |
| Azure OpenAI | `AzureOpenAiChatCompletionClient` | IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature |
| Azure AI Inference | `AzureAiInferenceChatCompletionClient` | IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature |
| Anthropic | `AnthropicChatCompletionClient` | IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature |
| Cohere | `CohereChatCompletionClient` | IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature, IGroundedChatFeature |

## Usage Examples

### Basic usage

```csharp
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello!")],
    Model: "gpt-4.1");

var response = await client.GetChatCompletionAsync(request);

if (response.IsSuccess)
    Console.WriteLine(response.Content);
else
    Console.WriteLine($"Error: {response.ErrorMessage}");
```

### With raw response

```csharp
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello!")],
    IncludeRawResponse: true);

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.RawRequestJson);
Console.WriteLine(response.RawResponseJson);
```

### With extra parameters

```csharp
var extra = JsonSerializer.SerializeToElement(new { top_p = 0.9, seed = 42 });
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello!")],
    ExtraParameters: extra);
```
