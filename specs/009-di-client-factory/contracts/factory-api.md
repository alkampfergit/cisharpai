# Factory API Contract (v2 — per-provider configuration hierarchy)

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

// Create a chat client at runtime — strongly-typed per-provider configuration
var config = new OpenAiClientConfiguration
{
    ApiKey = "sk-...",
    DefaultModel = "gpt-4o",
    Organization = "org-..."
};

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

### Azure Example (required fields)

```csharp
var azureConfig = new AzureOpenAiClientConfiguration
{
    ApiKey = "...",
    Endpoint = "https://my-resource.openai.azure.com/",
    DeploymentName = "gpt-4o",
    ApiVersion = "2024-02-15-preview"
};

var result = factory.CreateChatCompletionClient(azureConfig);
```

### Configuration Hierarchy

```
CisharpaiClientConfiguration (abstract, in Cisharpai core)
  ├── Provider: CisharpaiProvider (enum)
  └── ApiKey: string
       │
       ├── OpenAiClientConfiguration (in Cisharpai.OpenAi)
       │     BaseUrl, DefaultModel, Organization, ReasoningEffort, TextVerbosity
       │
       ├── AnthropicClientConfiguration (in Cisharpai.Anthropic)
       │     BaseUrl, ApiVersion, DefaultModel
       │
       ├── AzureOpenAiClientConfiguration (in Cisharpai.Azure)
       │     Endpoint, DeploymentName, ApiVersion, DefaultModel, ModelName, ReasoningEffort, TextVerbosity
       │
       ├── AzureAiInferenceClientConfiguration (in Cisharpai.Azure)
       │     Endpoint, ApiVersion, ModelId
       │
       └── CohereClientConfiguration (in Cisharpai.Cohere)
             BaseUrl, DefaultModel
```

The factory accepts the base type `CisharpaiClientConfiguration`, routes on `Provider` enum, then the provider implementation casts to its concrete subclass for strongly-typed access.

### Error Cases

| Scenario | IsSuccess | ErrorMessage |
|----------|-----------|-------------|
| Provider not registered | `false` | `"Provider 'Cohere' is not registered. Registered providers: OpenAi, Anthropic."` |
| Provider doesn't support embedding | `false` | `"Provider 'Anthropic' does not support embedding clients."` |
| Wrong concrete type for provider | `false` | `"Expected AzureOpenAiClientConfiguration for provider AzureOpenAi, got OpenAiClientConfiguration."` |
| Missing required config (e.g., AzureOpenAi without Endpoint) | compile-time error (`required` property) |
| Success | `true` | `null` |

### Provider Capabilities

| Provider | Chat Completion | Embedding |
|----------|:-:|:-:|
| OpenAi | Yes | Yes |
| AzureOpenAi | Yes | Yes |
| AzureAiInference | Yes | Yes |
| Anthropic | Yes | No |
| Cohere | Yes | Yes |
