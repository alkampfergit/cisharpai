# Quickstart: ExtraParameters & Deep Merge

## Prerequisites

- .NET 8.0 or .NET 10 SDK
- Any Cisharpai provider package

## 1. Add a New Provider Parameter

```csharp
using System.Text.Json;
using Cisharpai.Models;

var extra = JsonDocument.Parse("""{"top_p": 0.9, "frequency_penalty": 0.5}""").RootElement;

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Explain quantum computing")],
    Model: "gpt-4",
    Temperature: 0.7,
    ExtraParameters: extra);

var response = await client.GetChatCompletionAsync(request);
// The HTTP body contains: model, messages, temperature, top_p, frequency_penalty
```

## 2. Deep Merge Nested Objects

```csharp
// The library already sets reasoning.effort from typed options.
// Add reasoning.summary without losing effort:
var extra = JsonDocument.Parse("""{"reasoning": {"summary": "auto"}}""").RootElement;

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Solve this problem")],
    Model: "gpt-5",
    ExtraParameters: extra);

// Result: {"reasoning": {"effort": "medium", "summary": "auto"}, ...}
```

## 3. Override a Typed Property

```csharp
// Override max_tokens even though it's a typed parameter:
var extra = JsonDocument.Parse("""{"max_tokens": 8192}""").RootElement;

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Write a long essay")],
    Model: "claude-sonnet-4-20250514",
    MaxTokens: 1024,  // typed value
    ExtraParameters: extra);  // override wins: max_tokens = 8192
```

## 4. Inspect Raw Payloads

```csharp
var extra = JsonDocument.Parse("""{"top_p": 0.9}""").RootElement;

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello")],
    Model: "gpt-4",
    IncludeRawResponse: true,
    ExtraParameters: extra);

var response = await client.GetChatCompletionAsync(request);

// See exactly what was sent (post-merge)
Console.WriteLine($"Request:  {response.RawRequestJson}");
// See exactly what the provider returned
Console.WriteLine($"Response: {response.RawResponseJson}");
```

## 5. Anthropic-Specific Parameter

```csharp
// Anthropic's top_k is not in the unified API — use ExtraParameters:
var extra = JsonDocument.Parse("""{"top_k": 40}""").RootElement;

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello")],
    Model: "claude-sonnet-4-20250514",
    Temperature: 0.5,
    ExtraParameters: extra);

var response = await client.GetChatCompletionAsync(request);
```

## 6. Embedding Request with ExtraParameters

```csharp
var extra = JsonDocument.Parse("""{"truncation": "END"}""").RootElement;

var request = new EmbeddingRequest(
    Input: ["Hello world"],
    Model: "text-embedding-3-small",
    ExtraParameters: extra);

var response = await embeddingClient.GetEmbeddingsAsync(request);
```

## 7. Using JsonDeepMerge Directly

```csharp
using Cisharpai;

var baseJson = """{"model": "gpt-4", "reasoning": {"effort": "high"}}""";
var overrides = JsonDocument.Parse("""{"reasoning": {"summary": "auto"}, "stream": true}""").RootElement;

var merged = JsonDeepMerge.Merge(baseJson, overrides);
// Result: {"model":"gpt-4","reasoning":{"effort":"high","summary":"auto"},"stream":true}
```

## Verification

```bash
dotnet test src/Cisharpai.Tests/ --filter "FullyQualifiedName~JsonDeepMerge or FullyQualifiedName~ExtraParameters"
```
