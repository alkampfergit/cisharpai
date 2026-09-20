# Cisharpai.Anthropic

Anthropic (Claude) provider for the [Cisharpai](https://www.nuget.org/packages/Cisharpai) unified LLM client library.

## Features

- Chat completions with all Claude models
- JSON Mode (via system message injection) and Structured Outputs (`output_config.format`)
- Tool calling with `tool_use`/`tool_result` content blocks
- Vision (raw base64 image sources — PNG, JPEG, WebP, GIF)
- Streaming (event-based SSE: `message_start`, `content_block_delta`, `message_delta`)
- Refusal handling via `stop_reason: "refusal"`
- Built-in HTTP resilience (retries, timeouts)

## Quick Start

```csharp
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.Anthropic;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAnthropicClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
    options.DefaultModel = AnthropicModels.Chat.ClaudeSonnet4_5;
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();

var response = await client.GetChatCompletionAsync(
    new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "Explain monads in one sentence.")],
        MaxTokens: 200));

Console.WriteLine(response.Content);
```

## Available Models

`ClaudeOpus4_5`, `ClaudeSonnet4_5`, `ClaudeHaiku4_5`, `ClaudeSonnet4`, `ClaudeHaiku4`, `ClaudeOpus3`

## Provider Notes

- Anthropic uses **raw base64** for images (not data URIs). The client handles this automatically when you use `LlmMessage.WithImage()` or `WithBase64Image()`.
- System messages are extracted from the message list and sent as the top-level `system` parameter.
- Tool calling uses Anthropic's native `tool_use`/`tool_result` content block format.

## Links

- [GitHub](https://github.com/alkampfergit/cisharpai)
- [Provider Feature Matrix](https://github.com/alkampfergit/cisharpai/blob/main/wiki/provider-features.md)
