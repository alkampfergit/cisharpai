# Data Model: DI Client Factory

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

### RuntimeClientConfiguration (immutable record)

Provider-agnostic configuration for creating a client at runtime. Lives in `Cisharpai` core.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Provider` | `CisharpaiProvider` | Yes | Target provider |
| `ApiKey` | `string` | Yes | Authentication key |
| `Model` | `string?` | No | Default model name |
| `Endpoint` | `string?` | No | Custom endpoint URL (overrides provider default) |
| `ExtraSettings` | `IReadOnlyDictionary<string, string>?` | No | Provider-specific settings |

**ExtraSettings known keys per provider:**

| Provider | Key | Maps to |
|----------|-----|---------|
| OpenAi | `Organization` | `OpenAiClientOptions.Organization` |
| OpenAi | `ReasoningEffort` | `OpenAiClientOptions.ReasoningEffort` |
| OpenAi | `TextVerbosity` | `OpenAiClientOptions.TextVerbosity` |
| AzureOpenAi | `DeploymentName` | `AzureOpenAiClientOptions.DeploymentName` |
| AzureOpenAi | `ApiVersion` | `AzureOpenAiClientOptions.ApiVersion` |
| AzureOpenAi | `ModelName` | `AzureOpenAiClientOptions.ModelName` |
| AzureOpenAi | `ReasoningEffort` | `AzureOpenAiClientOptions.ReasoningEffort` |
| AzureOpenAi | `TextVerbosity` | `AzureOpenAiClientOptions.TextVerbosity` |
| AzureAiInference | `ApiVersion` | `AzureAiInferenceClientOptions.ApiVersion` |
| Anthropic | `ApiVersion` | `AnthropicClientOptions.ApiVersion` |

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

Both `Create*` methods receive `(IServiceProvider serviceProvider, RuntimeClientConfiguration configuration)`.

### ICisharpaiClientFactory (interface)

The consumer-facing factory. Lives in `Cisharpai` core.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `CreateChatCompletionClient` | `CisharpaiClientFactoryResult<IChatCompletionClient>` | Create a chat client |
| `CreateEmbeddingClient` | `CisharpaiClientFactoryResult<IEmbeddingClient>` | Create an embedding client |
| `GetRegisteredProviders` | `IReadOnlyCollection<CisharpaiProvider>` | List available providers |

Both `Create*` methods receive `(RuntimeClientConfiguration configuration)`.

### ICisharpaiClientFactoryBuilder (interface)

Fluent builder returned by `services.AddCisharpaiClientFactory()`. Used by provider extension methods to register their descriptors.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `AddProvider` | `ICisharpaiClientFactoryBuilder` | Register a provider descriptor |
| `Services` | `IServiceCollection` (property) | Access to the service collection for HttpClient registration |

## Relationships

```
RuntimeClientConfiguration ──uses──> CisharpaiProvider (enum value)
ICisharpaiClientFactory ──holds──> Dictionary<CisharpaiProvider, IClientFactoryProvider>
ICisharpaiClientFactory ──returns──> CisharpaiClientFactoryResult<T>
IClientFactoryProvider ──reads──> RuntimeClientConfiguration
IClientFactoryProvider ──uses──> IServiceProvider (for IHttpClientFactory, ILoggerFactory)
ICisharpaiClientFactoryBuilder ──builds──> ICisharpaiClientFactory
```
