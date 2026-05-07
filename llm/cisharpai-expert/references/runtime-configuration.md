# Runtime Configuration

Use when API keys, endpoints, or model IDs are not known at startup — e.g. multi-tenant systems where each tenant supplies their own credentials.

## Pattern: `Create` + `IHttpMessageHandlerFactory`

Register a named HTTP client once (connection pool only — no auth, no base address):

```csharp
// Program.cs
services.AddHttpClient("cisharpai");
// With resilience (recommended):
services.AddHttpClient("cisharpai").AddCisharpaiResilienceHandler();
```

Inject `IHttpMessageHandlerFactory` and call `Create` on demand:

```csharp
public class LlmClientService(
    IHttpMessageHandlerFactory handlerFactory,
    ILoggerFactory loggerFactory)
{
    public IChatCompletionClient CreateChat(TenantConfig cfg) => cfg.Provider switch
    {
        "openai"     => OpenAiChatCompletionClient.Create(handlerFactory,
                            new OpenAiClientOptions { ApiKey = cfg.ApiKey, DefaultModel = cfg.Model },
                            loggerFactory: loggerFactory),

        "anthropic"  => AnthropicChatCompletionClient.Create(handlerFactory,
                            new AnthropicClientOptions { ApiKey = cfg.ApiKey, DefaultModel = cfg.Model },
                            loggerFactory: loggerFactory),

        "cohere"     => CohereChatCompletionClient.Create(handlerFactory,
                            new CohereClientOptions { ApiKey = cfg.ApiKey, DefaultModel = cfg.Model },
                            loggerFactory: loggerFactory),

        "azure-openai" => AzureOpenAiChatCompletionClient.Create(handlerFactory,
                            new AzureOpenAiClientOptions
                            {
                                Endpoint       = cfg.Endpoint,
                                ApiKey         = cfg.ApiKey,
                                DeploymentName = cfg.DeploymentName,
                            },
                            loggerFactory: loggerFactory),

        "azure-inference" => AzureAiInferenceChatCompletionClient.Create(handlerFactory,
                            new AzureAiInferenceClientOptions
                            {
                                Endpoint = cfg.Endpoint,
                                ApiKey   = cfg.ApiKey,
                                ModelId  = cfg.ModelId,
                            },
                            loggerFactory: loggerFactory),

        _ => throw new NotSupportedException(cfg.Provider)
    };
}
```

## Factory signatures

Non-Azure providers:

```csharp
XxxClient.Create(
    IHttpMessageHandlerFactory handlerFactory,
    XxxClientOptions options,
    string handlerName = "cisharpai",
    ILoggerFactory? loggerFactory = null)
```

Azure providers (extra optional `TokenCredential` for Azure AD):

```csharp
AzureOpenAiChatCompletionClient.Create(
    IHttpMessageHandlerFactory handlerFactory,
    AzureOpenAiClientOptions options,
    TokenCredential? credential = null,
    string handlerName = "cisharpai",
    ILoggerFactory? loggerFactory = null)
```

## Available `Create` methods

| Package | Client |
|---------|--------|
| `Cisharpai.OpenAi` | `OpenAiChatCompletionClient`, `OpenAiEmbeddingClient` |
| `Cisharpai.Anthropic` | `AnthropicChatCompletionClient` |
| `Cisharpai.Cohere` | `CohereChatCompletionClient`, `CohereEmbeddingClient` |
| `Cisharpai.Azure` | `AzureOpenAiChatCompletionClient`, `AzureOpenAiEmbeddingClient`, `AzureAiInferenceChatCompletionClient`, `AzureAiInferenceEmbeddingClient` |

## Thread safety

All clients created via `Create` are **thread-safe for concurrent calls** — each call creates independent request state; only the pooled `HttpMessageHandler` is shared, and `HttpClient` is safe to call concurrently. You may share a single client instance across threads or create one per request — both are correct.

## Notes

- **Resilience**: `Create` does not auto-wire resilience handlers. Add `.AddCisharpaiResilienceHandler()` to the named client registration at startup.
- **Caching instances**: Client construction is cheap. Caching per `(provider, apiKey, endpoint)` is fine but not required.
- **Azure OpenAI learned routing cache**: chat clients share route-shape discoveries in-process per `(Endpoint, DeploymentName, ApiVersion)`, so creating a new Azure OpenAI client after a fallback does not repeat the same route-mismatch probe.
- **Azure AD at runtime**: Pass a `TokenCredential` (e.g. `ClientSecretCredential`) resolved at request time to `Create`.
