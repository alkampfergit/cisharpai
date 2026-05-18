# Client Factory

The **DI Client Factory** creates `IChatCompletionClient` and `IEmbeddingClient` instances at runtime from a provider-agnostic configuration object. Factory-created clients use the same DI-managed `HttpClient` with resilience handlers (retry, circuit breaker, timeout) as clients registered via the existing `Add*Client()` helpers.

## When to Use

Use the factory when you **don't know the provider at startup** — multi-tenant apps, user-configurable AI backends, or dynamic provider selection. If the provider is fixed at startup, the existing `services.AddOpenAiClient(...)` pattern is simpler.

## Registration

```csharp
var services = new ServiceCollection();

services.AddCisharpaiClientFactory()
    .AddOpenAiSupport()
    .AddAnthropicSupport()
    .AddAzureOpenAiSupport()
    .AddAzureAiInferenceSupport()
    .AddCohereSupport();

var provider = services.BuildServiceProvider();
```

Each `Add*Support()` call registers a named `HttpClient` with the standard Cisharpai resilience handler and an `IClientFactoryProvider` implementation for that provider.

## Creating Clients

```csharp
var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

// Strongly-typed per-provider configuration
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

## Configuration Hierarchy

Each provider has a strongly-typed configuration record inheriting from `CisharpaiClientConfiguration`:

| Provider | Configuration Class | Required Fields |
|----------|-------------------|----------------|
| OpenAI | `OpenAiClientConfiguration` | `ApiKey` |
| Anthropic | `AnthropicClientConfiguration` | `ApiKey` |
| Azure OpenAI | `AzureOpenAiClientConfiguration` | `ApiKey`, `Endpoint`, `DeploymentName` |
| Azure AI Inference | `AzureAiInferenceClientConfiguration` | `ApiKey`, `Endpoint` |
| Cohere | `CohereClientConfiguration` | `ApiKey` |

The factory routes on the `Provider` enum (derived from the concrete configuration type).

## Embedding Clients

```csharp
var embeddingConfig = new OpenAiClientConfiguration
{
    ApiKey = "sk-...",
    DefaultModel = "text-embedding-3-small"
};

var result = factory.CreateEmbeddingClient(embeddingConfig);
```

Not all providers support embeddings. Anthropic returns `IsSuccess=false` with a descriptive error.

## Error Handling

The factory follows the library's **no-exceptions-for-API-errors** pattern:

| Scenario | IsSuccess | ErrorMessage |
|----------|-----------|-------------|
| Provider not registered | `false` | `"Provider 'Cohere' is not registered..."` |
| Provider doesn't support embedding | `false` | `"Provider 'Anthropic' does not support embedding clients."` |
| Wrong config type for provider | `false` | `"Expected AzureOpenAiClientConfiguration for provider AzureOpenAi, got OpenAiClientConfiguration."` |
| Success | `true` | `null` |

## Available Providers

```csharp
var registered = factory.GetRegisteredProviders();
// IReadOnlyCollection<CisharpaiProvider>
```

## Thread Safety

The factory is thread-safe. Concurrent calls with different configurations produce independent client instances.
