# Data Model: DI Client Factory (v2 — per-provider configuration hierarchy)

## Entities

### CisharpaiProvider (enum)

Identifies a supported LLM provider. Lives in `Cisharpai` core.

| Value | Description |
|-------|-------------|
| `OpenAi` | OpenAI API |
| `AzureOpenAi` | Azure OpenAI Service |
| `AzureAiInference` | Azure AI Inference (Model-as-a-Service) |
| `Anthropic` | Anthropic (Claude) |
| `Cohere` | Cohere |

### CisharpaiClientConfiguration (abstract record — base)

Base configuration with only two fields: provider identity and API key. Lives in `Cisharpai` core. Each provider defines a concrete subclass with provider-specific settings.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Provider` | `CisharpaiProvider` | Yes | Target provider |
| `ApiKey` | `string` | Yes | Authentication key |

The factory routes on `Provider` enum; the concrete subclass is consumed only by the matching `IClientFactoryProvider` implementation.

### OpenAiClientConfiguration (record, extends CisharpaiClientConfiguration)

Lives in `Cisharpai.OpenAi`. Mirrors `OpenAiClientOptions`.

| Field | Type | Required | Default | Maps to |
|-------|------|----------|---------|---------|
| `BaseUrl` | `string` | No | `https://api.openai.com/v1/` | `OpenAiClientOptions.BaseUrl` |
| `DefaultModel` | `string?` | No | `null` | `OpenAiClientOptions.DefaultModel` |
| `Organization` | `string?` | No | `null` | `OpenAiClientOptions.Organization` |
| `ReasoningEffort` | `string?` | No | `null` | `OpenAiClientOptions.ReasoningEffort` |
| `TextVerbosity` | `string?` | No | `null` | `OpenAiClientOptions.TextVerbosity` |

### AnthropicClientConfiguration (record, extends CisharpaiClientConfiguration)

Lives in `Cisharpai.Anthropic`. Mirrors `AnthropicClientOptions`.

| Field | Type | Required | Default | Maps to |
|-------|------|----------|---------|---------|
| `BaseUrl` | `string` | No | `https://api.anthropic.com/v1/` | `AnthropicClientOptions.BaseUrl` |
| `ApiVersion` | `string` | No | `2023-06-01` | `AnthropicClientOptions.ApiVersion` |
| `DefaultModel` | `string?` | No | `null` | `AnthropicClientOptions.DefaultModel` |

### AzureOpenAiClientConfiguration (record, extends CisharpaiClientConfiguration)

Lives in `Cisharpai.Azure`. Mirrors `AzureOpenAiClientOptions`.

| Field | Type | Required | Default | Maps to |
|-------|------|----------|---------|---------|
| `Endpoint` | `string` | Yes | — | `AzureOpenAiClientOptions.Endpoint` |
| `DeploymentName` | `string` | Yes | — | `AzureOpenAiClientOptions.DeploymentName` |
| `ApiVersion` | `string` | No | `""` | `AzureOpenAiClientOptions.ApiVersion` |
| `DefaultModel` | `string?` | No | `null` | `AzureOpenAiClientOptions.DefaultModel` |
| `ModelName` | `string?` | No | `null` | `AzureOpenAiClientOptions.ModelName` |
| `ReasoningEffort` | `string?` | No | `null` | `AzureOpenAiClientOptions.ReasoningEffort` |
| `TextVerbosity` | `string?` | No | `null` | `AzureOpenAiClientOptions.TextVerbosity` |

### AzureAiInferenceClientConfiguration (record, extends CisharpaiClientConfiguration)

Lives in `Cisharpai.Azure`. Mirrors `AzureAiInferenceClientOptions`.

| Field | Type | Required | Default | Maps to |
|-------|------|----------|---------|---------|
| `Endpoint` | `string` | Yes | — | `AzureAiInferenceClientOptions.Endpoint` |
| `ApiVersion` | `string` | No | `""` | `AzureAiInferenceClientOptions.ApiVersion` |
| `ModelId` | `string` | No | `""` | `AzureAiInferenceClientOptions.ModelId` |

### CohereClientConfiguration (record, extends CisharpaiClientConfiguration)

Lives in `Cisharpai.Cohere`. Mirrors `CohereClientOptions`.

| Field | Type | Required | Default | Maps to |
|-------|------|----------|---------|---------|
| `BaseUrl` | `string` | No | `https://api.cohere.com/v2/` | `CohereClientOptions.BaseUrl` |
| `DefaultModel` | `string?` | No | `null` | `CohereClientOptions.DefaultModel` |

### CisharpaiClientFactoryResult\<T\> (immutable record)

Result wrapper for factory operations. Lives in `Cisharpai` core.

| Field | Type | Description |
|-------|------|-------------|
| `IsSuccess` | `bool` | Whether client creation succeeded |
| `Client` | `T?` | The created client (null on failure) |
| `ErrorMessage` | `string?` | Error description (null on success) |

**Static factory methods:**
- `Success(T client)` — creates a successful result
- `Failure(string errorMessage)` — creates a failure result

### IClientFactoryProvider (interface)

Provider descriptor that knows how to create clients from a `RuntimeClientConfiguration`. Lives in `Cisharpai` core, implemented by each provider project.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Provider` | `CisharpaiProvider` (property) | Which provider this descriptor handles |
| `SupportsChatCompletion` | `bool` (property) | Whether this provider can create chat clients |
| `SupportsEmbedding` | `bool` (property) | Whether this provider can create embedding clients |
| `CreateChatCompletionClient` | `CisharpaiClientFactoryResult<IChatCompletionClient>` | Create a chat client from config |
| `CreateEmbeddingClient` | `CisharpaiClientFactoryResult<IEmbeddingClient>` | Create an embedding client from config |

Both `Create*` methods receive `(IServiceProvider serviceProvider, CisharpaiClientConfiguration configuration)`. Each provider implementation casts to its concrete subclass.

### ICisharpaiClientFactory (interface)

The consumer-facing factory. Lives in `Cisharpai` core.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `CreateChatCompletionClient` | `CisharpaiClientFactoryResult<IChatCompletionClient>` | Create a chat client |
| `CreateEmbeddingClient` | `CisharpaiClientFactoryResult<IEmbeddingClient>` | Create an embedding client |
| `GetRegisteredProviders` | `IReadOnlyCollection<CisharpaiProvider>` | List available providers |

Both `Create*` methods receive `(CisharpaiClientConfiguration configuration)`.

### ICisharpaiClientFactoryBuilder (interface)

Fluent builder returned by `services.AddCisharpaiClientFactory()`. Used by provider extension methods to register their descriptors.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `AddProvider` | `ICisharpaiClientFactoryBuilder` | Register a provider descriptor |
| `Services` | `IServiceCollection` (property) | Access to the service collection for HttpClient registration |

## Relationships

```
CisharpaiClientConfiguration (base) ──uses──> CisharpaiProvider (enum value)
  ├── OpenAiClientConfiguration
  ├── AnthropicClientConfiguration
  ├── AzureOpenAiClientConfiguration
  ├── AzureAiInferenceClientConfiguration
  └── CohereClientConfiguration

ICisharpaiClientFactory ──holds──> Dictionary<CisharpaiProvider, IClientFactoryProvider>
ICisharpaiClientFactory ──accepts──> CisharpaiClientConfiguration (base)
ICisharpaiClientFactory ──returns──> CisharpaiClientFactoryResult<T>
IClientFactoryProvider ──casts──> concrete configuration subclass
IClientFactoryProvider ──uses──> IServiceProvider (for IHttpClientFactory, ILoggerFactory)
ICisharpaiClientFactoryBuilder ──builds──> ICisharpaiClientFactory
```
