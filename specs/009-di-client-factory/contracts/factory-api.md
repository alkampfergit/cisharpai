# Factory API Contract

## Public API Surface

### Registration (at startup)

```csharp
// Entry point — returns builder for fluent provider registration
services.AddCisharpaiClientFactory()
    .AddOpenAiSupport()
    .AddAnthropicSupport()
    .AddAzureOpenAiSupport()
    .AddAzureAiInferenceSupport()
    .AddCohereSupport();
```

Each `Add*Support()` method:
1. Registers a named HttpClient with resilience handler for that provider's factory path
2. Registers the provider's `IClientFactoryProvider` implementation

### Runtime Usage

```csharp
// Resolve factory from DI
var factory = serviceProvider.GetRequiredService<ICisharpaiClientFactory>();

// Create a chat client at runtime
var config = new RuntimeClientConfiguration(
    Provider: CisharpaiProvider.OpenAi,
    ApiKey: "sk-...",
    Model: "gpt-4o"
);

var result = factory.CreateChatCompletionClient(config);
if (result.IsSuccess)
{
    var response = await result.Client!.GetChatCompletionAsync(request);
}
else
{
    Console.WriteLine(result.ErrorMessage);
}
```

### Error Cases

| Scenario | IsSuccess | ErrorMessage |
|----------|-----------|-------------|
| Provider not registered | `false` | `"Provider 'Cohere' is not registered. Registered providers: OpenAi, Anthropic."` |
| Provider doesn't support embedding | `false` | `"Provider 'Anthropic' does not support embedding clients."` |
| Missing required config (e.g., AzureOpenAi without DeploymentName) | `false` | `"AzureOpenAi requires 'DeploymentName' in ExtraSettings."` |
| Success | `true` | `null` |

### Provider Capabilities

| Provider | Chat Completion | Embedding |
|----------|:-:|:-:|
| OpenAi | Yes | Yes |
| AzureOpenAi | Yes | Yes |
| AzureAiInference | Yes | Yes |
| Anthropic | Yes | No |
| Cohere | Yes | Yes |

### ExtraSettings Contract

Provider-specific settings are passed via the `ExtraSettings` dictionary on `RuntimeClientConfiguration`. Keys are case-sensitive strings matching the property names documented in data-model.md.

Missing optional keys use provider defaults (same defaults as the existing `*ClientOptions` classes). Missing required keys (e.g., `DeploymentName` for AzureOpenAi) result in a failure result, not an exception.
