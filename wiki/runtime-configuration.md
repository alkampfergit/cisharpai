# Runtime Configuration

This guide covers how to create provider clients at runtime when connection details (API keys, endpoints, model IDs) are not known at startup — for example, in multi-tenant systems where each tenant may configure their own LLM provider.

## The problem with startup-time DI

The standard `Add…Client` DI helpers require all options to be fixed at `Startup`/`Program.cs`. When configuration comes from a database, vault, or user settings at request time, you need to create clients dynamically without re-registering services.

## Solution: `Create` factory methods + `IHttpMessageHandlerFactory`

Every client exposes a static `Create` method that accepts an `IHttpMessageHandlerFactory`. The factory provides a pooled `HttpMessageHandler` (managing TCP connection reuse), while the provider-specific auth handler is applied per-client-instance. This avoids socket exhaustion while still allowing fully dynamic configuration.

### 1. Register the pooled handler (once, at startup)

```csharp
// Program.cs — no provider-specific config needed here
services.AddHttpClient("cisharpai");
```

`IHttpMessageHandlerFactory` is automatically registered by `AddHttpClient`. The named entry `"cisharpai"` is a pure connection-pool slot — no auth, no base address.

### 2. Inject `IHttpMessageHandlerFactory` and create clients on demand

```csharp
public class LlmClientService(
    IHttpMessageHandlerFactory handlerFactory,
    ILoggerFactory loggerFactory)
{
    public IChatCompletionClient CreateChat(TenantConfig cfg) => cfg.Provider switch
    {
        "openai"    => OpenAiChatCompletionClient.Create(handlerFactory,
                           new OpenAiClientOptions { ApiKey = cfg.ApiKey, DefaultModel = cfg.Model },
                           loggerFactory: loggerFactory),

        "anthropic" => AnthropicChatCompletionClient.Create(handlerFactory,
                           new AnthropicClientOptions { ApiKey = cfg.ApiKey, DefaultModel = cfg.Model },
                           loggerFactory: loggerFactory),

        "cohere"    => CohereChatCompletionClient.Create(handlerFactory,
                           new CohereClientOptions { ApiKey = cfg.ApiKey, DefaultModel = cfg.Model },
                           loggerFactory: loggerFactory),

        "azure-openai" => AzureOpenAiChatCompletionClient.Create(handlerFactory,
                           new AzureOpenAiClientOptions
                           {
                               Endpoint       = cfg.Endpoint,
                               ApiKey         = cfg.ApiKey,
                               DeploymentName = cfg.DeploymentName
                           },
                           loggerFactory: loggerFactory),

        "azure-inference" => AzureAiInferenceChatCompletionClient.Create(handlerFactory,
                           new AzureAiInferenceClientOptions
                           {
                               Endpoint = cfg.Endpoint,
                               ApiKey   = cfg.ApiKey,
                               ModelId  = cfg.ModelId
                           },
                           loggerFactory: loggerFactory),

        _ => throw new NotSupportedException(cfg.Provider)
    };
}
```

## Factory method signatures

All non-Azure providers:

```csharp
XxxClient.Create(
    IHttpMessageHandlerFactory handlerFactory,
    XxxClientOptions options,
    string handlerName = "cisharpai",   // matches the registered named HttpClient
    ILoggerFactory? loggerFactory = null)
```

Azure providers additionally accept an optional `TokenCredential` for Azure AD authentication:

```csharp
AzureOpenAiChatCompletionClient.Create(
    IHttpMessageHandlerFactory handlerFactory,
    AzureOpenAiClientOptions options,
    TokenCredential? credential = null,  // omit to use ApiKey from options
    string handlerName = "cisharpai",
    ILoggerFactory? loggerFactory = null)
```

## Azure AD authentication at runtime

```csharp
// Credential resolved at runtime (e.g. from a per-tenant app registration)
TokenCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

var client = AzureOpenAiChatCompletionClient.Create(
    handlerFactory,
    new AzureOpenAiClientOptions { Endpoint = endpoint, DeploymentName = deployment },
    credential: credential);
```

## Available `Create` methods

| Package | Client | Method |
|---------|--------|--------|
| `Cisharpai.OpenAi` | `OpenAiChatCompletionClient` | `Create(handlerFactory, OpenAiClientOptions, ...)` |
| `Cisharpai.OpenAi` | `OpenAiEmbeddingClient` | `Create(handlerFactory, OpenAiClientOptions, ...)` |
| `Cisharpai.Anthropic` | `AnthropicChatCompletionClient` | `Create(handlerFactory, AnthropicClientOptions, ...)` |
| `Cisharpai.Cohere` | `CohereChatCompletionClient` | `Create(handlerFactory, CohereClientOptions, ...)` |
| `Cisharpai.Cohere` | `CohereEmbeddingClient` | `Create(handlerFactory, CohereClientOptions, ...)` |
| `Cisharpai.Azure` | `AzureOpenAiChatCompletionClient` | `Create(handlerFactory, AzureOpenAiClientOptions, credential?, ...)` |
| `Cisharpai.Azure` | `AzureOpenAiEmbeddingClient` | `Create(handlerFactory, AzureOpenAiClientOptions, credential?, ...)` |
| `Cisharpai.Azure` | `AzureAiInferenceChatCompletionClient` | `Create(handlerFactory, AzureAiInferenceClientOptions, credential?, ...)` |
| `Cisharpai.Azure` | `AzureAiInferenceEmbeddingClient` | `Create(handlerFactory, AzureAiInferenceClientOptions, credential?, ...)` |

## Using a custom handler pool name

If you want separate connection pools for different providers, register multiple named clients and pass the matching `handlerName`:

```csharp
services.AddHttpClient("cisharpai-openai");
services.AddHttpClient("cisharpai-azure");

var openAiClient = OpenAiChatCompletionClient.Create(
    handlerFactory, options, handlerName: "cisharpai-openai");

var azureClient = AzureOpenAiChatCompletionClient.Create(
    handlerFactory, azureOptions, handlerName: "cisharpai-azure");
```

## Notes

- **Client lifetime**: `Create` returns a new client instance each call. The client is cheap to construct; the underlying TCP connections are pooled in the handler. You may cache client instances per unique `(provider, apiKey, baseUrl)` tuple if desired — but it is not required.
- **Logging and telemetry**: Pass `ILoggerFactory` to get structured EventId 1000–1005 logs and `Cisharpai` `ActivitySource` spans (see [Logging](logging.md)).
- **Features**: Clients created via `Create` expose the same `IFeatureCollection` features (streaming, tool calling, JSON output, etc.) as DI-registered clients.
- **Resilience handlers**: The DI helpers (`AddOpenAiClient`, etc.) automatically wire `AddCisharpaiResilienceHandler()`. `Create` bypasses DI, so no resilience policy is applied by default. Add one to the named HttpClient registration if needed: `services.AddHttpClient("cisharpai").AddCisharpaiResilienceHandler()`.
