# Quickstart: Streaming Chat Completions

## Setup

Add the provider package you need:

```bash
dotnet add package Cisharpai.OpenAi
# or: Cisharpai.Azure, Cisharpai.Anthropic, Cisharpai.Cohere
```

Register the client via DI:

```csharp
services.AddOpenAiClient(options =>
{
    options.ApiKey = "your-api-key";
});
```

## Basic Streaming

```csharp
using Cisharpai;
using Cisharpai.Features.Chat;
using Cisharpai.Models;

IChatCompletionClient client = provider.GetRequiredService<IChatCompletionClient>();

// Discover streaming feature
var streamFeature = client.Features.Get<IStreamingChatFeature>();
if (streamFeature is null)
{
    Console.WriteLine("Streaming not supported.");
    return;
}

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Write a poem about the sea")],
    Model: "gpt-4.1-nano",
    MaxTokens: 500);

// Stream tokens as they arrive
await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
{
    if (!string.IsNullOrEmpty(chunk.Content))
        Console.Write(chunk.Content);
}
Console.WriteLine();
```

## Accumulating Content and Usage

```csharp
var sb = new StringBuilder();
string? finishReason = null;
int? promptTokens = null;
int? completionTokens = null;

await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
{
    sb.Append(chunk.Content);

    if (chunk.FinishReason is not null)
        finishReason = chunk.FinishReason;
    if (chunk.PromptTokens.HasValue)
        promptTokens = chunk.PromptTokens;
    if (chunk.CompletionTokens.HasValue)
        completionTokens = chunk.CompletionTokens;
}

Console.WriteLine($"Response: {sb}");
Console.WriteLine($"Finish: {finishReason}, Tokens: {promptTokens}+{completionTokens}");
```

## Cancellation

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(30));

try
{
    await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request, cts.Token))
    {
        Console.Write(chunk.Content);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n[Stream cancelled]");
}
```

## Streaming Resilience (Production)

The standard resilience handler's 60s/90s timeouts terminate long streams.
Use the streaming-specific handler:

```csharp
services.AddHttpClient<IChatCompletionClient>("openai")
    .AddCisharpaiStreamingResilienceHandler();
```

This disables timeout constraints while keeping retry and circuit-breaker policies.

## Unit Testing with FakeChatCompletionClient

```csharp
using Cisharpai.Testing;
using Cisharpai.Features.Chat;
using Cisharpai.Models;

var fakeClient = new FakeChatCompletionClient(FakeChatFeatures.Streaming);

fakeClient.EnqueueStreamingResponse(new List<ChatCompletionChunk>
{
    new("Hello"),
    new(", world!"),
    new(string.Empty, FinishReason: "stop", PromptTokens: 10, CompletionTokens: 5)
});

var feature = fakeClient.Features.Get<IStreamingChatFeature>()!;
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hi")],
    Model: "test-model");

var chunks = new List<ChatCompletionChunk>();
await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
{
    chunks.Add(chunk);
}

// Assert
Assert.That(chunks, Has.Count.EqualTo(3));
Assert.That(fakeClient.StreamingCallCount, Is.EqualTo(1));
Assert.That(fakeClient.ReceivedStreamingRequests[0].Model, Is.EqualTo("test-model"));
```

## How to Verify It Works

1. Run the console demo: select "OpenAI streaming" from the menu
2. Run unit tests: `dotnet test --filter "FullyQualifiedName~Streaming"`
3. Run integration tests: `dotnet test --filter "FullyQualifiedName~StreamingIntegration"`
