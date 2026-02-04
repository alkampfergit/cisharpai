# Cisharpai

Cisharpai is a unified .NET client library for chat completions across multiple LLM providers. It exposes a single interface so you can switch providers (OpenAI, Azure OpenAI, Anthropic) with minimal code changes.

## Why Cisharpai?

- One shared `IChatCompletionClient` interface
- Unified request/response models
- Provider-specific packages for OpenAI, Azure OpenAI, and Anthropic
- Built-in HTTP resilience for retries and timeouts

## Quick start

1) Add references to the core library and a provider package:

- Cisharpai
- Cisharpai.OpenAi or Cisharpai.AzureOpenAi or Cisharpai.Anthropic

2) Register and call the client:

```csharp
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddOpenAiClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Say hello in one sentence.")],
    Model: "gpt-4.1-nano",
    Temperature: 0.2,
    MaxTokens: 100);

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.Content);
```

## Documentation

Start here:

- [wiki/index.md](wiki/index.md)
- [wiki/getting-started.md](wiki/getting-started.md)
- [wiki/openai.md](wiki/openai.md)

## Samples

- OpenAI console scenario: [src/Cisharp.Console/Scenarios/OpenAiChatScenario.cs](src/Cisharp.Console/Scenarios/OpenAiChatScenario.cs)

## License

See the repository license file for terms.
