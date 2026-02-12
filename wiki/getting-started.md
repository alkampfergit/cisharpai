# Getting started

This guide shows the basic workflow for using Cisharpai with any provider implementation.

## 1) Add references

Add references to the core library and the provider of your choice:

- Cisharpai (core abstractions)
- Cisharpai.OpenAi, Cisharpai.Azure, Cisharpai.Anthropic, or Cisharpai.Cohere (provider implementation)

## 2) Register a provider client

Use dependency injection to register the provider-specific client. For example, with OpenAI:

```csharp
using Cisharpai;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddOpenAiClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();
```

## 3) Send a chat completion request

Create a request using the shared models and call `IChatCompletionClient`:

```csharp
using Cisharpai.Models;

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Say hello in one sentence.")],
    Model: "gpt-4.1-nano",
    Temperature: 0.2,
    MaxTokens: 100);

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.Content);
```

## Next steps

- Follow the OpenAI-specific guide in [wiki/openai.md](openai.md).
- Browse the console sample in [src/Cisharp.Console/Scenarios](../src/Cisharp.Console/Scenarios) for end-to-end usage examples.
