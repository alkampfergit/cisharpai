# Contract: IGroundedChatFeature

**File**: `src/Cisharpai/Features/Chat/IGroundedChatFeature.cs`

## Interface Definition

```csharp
public interface IGroundedChatFeature
{
    Task<GroundedChatCompletionResponse> GetGroundedChatCompletionAsync(
        ChatCompletionRequest request,
        GroundedChatOptions groundedChatOptions,
        CancellationToken cancellationToken = default);
}
```

## Behavior

### `GetGroundedChatCompletionAsync`

Sends a chat completion request grounded on provided documents, returning the response with source citations.

**Input**:
- `request`: Standard `ChatCompletionRequest` (messages, model, temperature, etc.)
- `groundedChatOptions`: Documents to ground on and citation mode.

**Output**: `GroundedChatCompletionResponse` containing:
- `ChatCompletion`: The standard chat response (content, tokens, raw JSON)
- `Citations`: Character-offset citations linking response spans to source documents

**Validation** (performed before HTTP call):
- `groundedChatOptions.Validate()` — at least one document required
- Each `DocumentChunk.Validate()` — exactly one of Data or Text

**Error behavior**:
- Validation failure: Caught internally → `IsSuccess=false` with error message
- HTTP errors: Caught internally → `IsSuccess=false` with error message and raw response JSON
- `CitationMode.Accurate` on `command-a` model: Silently downgraded to `Fast` with log warning

**Provider-specific behavior**:
- Does NOT set `response_format` (mutually exclusive with JSON Mode at Cohere API level)
- Posts to the standard `/chat` endpoint with added `documents` and `citation_options` fields

## Discovery

```csharp
if (client.Features.Get<IGroundedChatFeature>() is { } groundedFeature)
{
    var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);
}
```

## Availability

| Provider | Available | Notes |
|----------|-----------|-------|
| OpenAI | No | — |
| Azure OpenAI | No | — |
| Azure AI Inference | No | — |
| Anthropic | No | — |
| Cohere | Yes | Command-R, Command-R+, Command-A models |
| Fake (testing) | Configurable | Enabled with `FakeChatFeatures.GroundedChat` |
