# Anthropic Provider

## Setup

```csharp
services.AddAnthropicClient(options =>
{
    options.ApiKey = "sk-ant-...";
    options.BaseUrl = "https://api.anthropic.com";  // default
    options.ApiVersion = "2023-06-01";               // default
    options.DefaultModel = "claude-sonnet-4-5-20250514";
});
```

**Options:**
- `ApiKey` (required)
- `BaseUrl` — Override for proxies
- `ApiVersion` — Anthropic API version header
- `DefaultModel` — Fallback model

## Available Models (`AnthropicModels`)

- `AnthropicModels.Chat.ClaudeOpus4_5` — `claude-opus-4-5-20250514`
- `AnthropicModels.Chat.ClaudeSonnet4_5` — `claude-sonnet-4-5-20250514`
- `AnthropicModels.Chat.ClaudeHaiku4_5` — `claude-haiku-4-5-20251015`

## Supported Features

- `IJsonOutputFeature` — JSON Mode via `output_config.format`
- `IToolCallingFeature` — `tool_use` / `tool_result` content blocks
- `IStreamingChatFeature` — Event-based SSE (no `[DONE]`)
- Vision — Raw base64 (NOT data URIs)

## Important: Image Handling

Anthropic uses raw base64 in a `source` object, **not** data URIs:

```json
{
  "type": "image",
  "source": {
    "type": "base64",
    "media_type": "image/png",
    "data": "<raw-base64>"
  }
}
```

Cisharpai handles this conversion automatically when you use:
```csharp
var msg = LlmMessage.WithImage("Describe this", "photo.png");
var msg = LlmMessage.WithBase64Image("Describe this", base64Data, "image/png");
```

## Tool Calling Differences

Anthropic uses content blocks instead of the OpenAI-style `tools` array:

- **Tool use:** Model returns `tool_use` content blocks with `id`, `name`, `input`
- **Tool result:** User sends `tool_result` content blocks referencing the tool use `id`

Cisharpai normalizes this behind `IToolCallingFeature` — you use the same `ToolDefinition`, `ToolCall`, and `ToolCallingOptions` as other providers.

## Streaming Differences

Anthropic uses event-based SSE without a `[DONE]` sentinel:

- `message_start` — Initial message metadata
- `content_block_start` — New content block begins
- `content_block_delta` — Token content
- `message_delta` — Final usage stats
- `message_stop` — Stream complete

Cisharpai normalizes these into `ChatCompletionChunk` objects.

## JSON Output

Anthropic uses `output_config.format` for JSON mode:

```csharp
var jsonFeature = client.Features.Get<IJsonOutputFeature>();
var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(
    request,
    new JsonOutputOptions(Mode: JsonOutputMode.JsonObject));
```

Structured Outputs with schema are also supported.
