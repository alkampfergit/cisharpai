# Quickstart: Standard Chat Completion

## Setup

Add the core package and at least one provider to your project:

```xml
<PackageReference Include="Cisharpai" />
<PackageReference Include="Cisharpai.OpenAi" />          <!-- or any provider -->
```

## Option A: Dependency Injection (Recommended)

Register a provider in your DI container:

```csharp
using Cisharpai.OpenAi;

services.AddOpenAiClient(o =>
{
    o.ApiKey = "sk-...";
    o.DefaultModel = "gpt-4.1";
});
```

Resolve and use:

```csharp
using Cisharpai;
using Cisharpai.Models;

var client = provider.GetRequiredService<IChatCompletionClient>();

var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Say hello in one sentence.")]));

if (response.IsSuccess)
    Console.WriteLine(response.Content);
else
    Console.WriteLine($"Error: {response.ErrorMessage}");
```

## Option B: Runtime / Factory Creation

For multi-tenant or dynamic scenarios:

```csharp
using Cisharpai.OpenAi;

var client = OpenAiChatCompletionClient.Create(
    handlerFactory,
    new OpenAiClientOptions { ApiKey = "sk-...", DefaultModel = "gpt-4.1" });
```

## Switching Providers

Change only the registration — application code stays identical:

```csharp
// Switch to Anthropic
services.AddAnthropicClient(o =>
{
    o.ApiKey = "sk-ant-...";
    o.DefaultModel = "claude-sonnet-4-5-20250514";
});

// Switch to Azure OpenAI
services.AddAzureOpenAiClient(o =>
{
    o.Endpoint = "https://myresource.openai.azure.com";
    o.DeploymentName = "gpt-4o";
    o.ApiKey = "...";
});

// Switch to Cohere
services.AddCohereChatClient(o =>
{
    o.ApiKey = "...";
    o.DefaultModel = "command-a-08-2025";
});
```

## Multi-Turn Conversation

```csharp
var request = new ChatCompletionRequest(
    Messages:
    [
        new LlmMessage(LlmRole.System, "You are a helpful assistant."),
        new LlmMessage(LlmRole.User, "What is the capital of France?"),
        new LlmMessage(LlmRole.Assistant, "The capital of France is Paris."),
        new LlmMessage(LlmRole.User, "What is its population?")
    ],
    Temperature: 0.2,
    MaxTokens: 200);

var response = await client.GetChatCompletionAsync(request);
```

## Debugging: Raw JSON

```csharp
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello")],
    IncludeRawResponse: true);

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine($"Request:  {response.RawRequestJson}");
Console.WriteLine($"Response: {response.RawResponseJson}");
```

## Escape Hatch: ExtraParameters

Inject provider-specific fields not yet in the unified API:

```csharp
using System.Text.Json;

var extra = JsonSerializer.SerializeToElement(new { top_p = 0.9, seed = 42 });
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello")],
    ExtraParameters: extra);
```

## Discover Optional Features

```csharp
// Check if streaming is supported
if (client.Features.Get<IStreamingChatFeature>() is { } streaming)
{
    await foreach (var chunk in streaming.GetChatCompletionStreamAsync(request))
        Console.Write(chunk.Content);
}
```

## Multiple Providers (Keyed DI)

```csharp
services.AddOpenAiClient("openai", o => { o.ApiKey = "sk-..."; });
services.AddAnthropicClient("anthropic", o => { o.ApiKey = "sk-ant-..."; });

var openAi = provider.GetRequiredKeyedService<IChatCompletionClient>("openai");
var claude = provider.GetRequiredKeyedService<IChatCompletionClient>("anthropic");
```

## Unit Testing with Fakes

```csharp
using Cisharpai.Testing;

var fake = new FakeChatCompletionClient();
fake.DefaultResponse = new ChatCompletionResponse(
    Content: "Hello!",
    Model: "test-model",
    PromptTokens: 5,
    CompletionTokens: 2);

var response = await fake.GetChatCompletionAsync(request);
Assert.That(response.Content, Is.EqualTo("Hello!"));
Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
```

## Verify It Works

1. Set your API key in environment variables or user secrets.
2. Run the console demo: `dotnet run --project src/Cisharp.Console`
3. Select "OpenAI Chat" (or any provider) from the Spectre.Console menu.
4. Verify you get a response with content and token counts.
