# Cisharpai

Cisharpai is a unified .NET client library for interacting with multiple LLM providers. It exposes shared interfaces so you can switch providers (OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere) with minimal code changes.

## Why Cisharpai?

- One shared `IChatCompletionClient` and `IEmbeddingClient` interface
- Unified request/response models across all providers
- Provider-specific packages: `Cisharpai.OpenAi`, `Cisharpai.Azure`, `Cisharpai.Anthropic`, `Cisharpai.Cohere`
- Built-in HTTP resilience for retries and timeouts
- Feature Collection pattern for optional capabilities: JSON output, tool calling, grounded chat (RAG), image embeddings, multimodal embeddings
- No exceptions for API errors -- consistent `IsSuccess`/`ErrorMessage` error handling
- Full debug support with `RawRequestJson`/`RawResponseJson`

## Quick start

1) Add references to the core library and a provider package:

- Cisharpai
- Cisharpai.OpenAi or Cisharpai.Azure or Cisharpai.Anthropic or Cisharpai.Cohere

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

## Dynamic Provider Selection

When you don't know the provider at startup — multi-tenant apps, user-configurable backends, or A/B testing across models — use the **Client Factory**:

```csharp
using Cisharpai;
using Cisharpai.Anthropic;
using Cisharpai.Azure;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCisharpaiClientFactory()
    .AddAnthropicSupport()
    .AddAzureAiInferenceSupport();

var sp = services.BuildServiceProvider();
var factory = sp.GetRequiredService<ICisharpaiClientFactory>();

// Create clients at runtime with provider-specific configuration
var result = factory.CreateChatCompletionClient(new AnthropicClientConfiguration
{
    ApiKey = "sk-ant-...",
    DefaultModel = "claude-sonnet-4-5-20250514"
});

if (result.IsSuccess)
{
    var response = await result.Client!.GetChatCompletionAsync(request);
}
```

Each provider has a strongly-typed configuration record (`OpenAiClientConfiguration`, `AnthropicClientConfiguration`, `AzureOpenAiClientConfiguration`, `AzureAiInferenceClientConfiguration`, `CohereClientConfiguration`). The factory routes to the correct provider automatically.

See the [Client Factory guide](wiki/factory.md) for the full configuration hierarchy, error handling, and embedding client support.

## Documentation

Start here:

- [Getting Started](wiki/getting-started.md)
- [Provider Feature Matrix](wiki/provider-features.md) -- see what each provider supports
- [OpenAI Quickstart](wiki/openai.md)
- [Embeddings](wiki/embeddings.md) -- text, image, and multimodal embeddings across providers
- [JSON Output](wiki/json-output.md) -- JSON Mode and Structured Outputs
- [Tool Calling](wiki/tool-calling.md) -- function calling across providers
- [Grounded Chat (RAG)](wiki/grounded-chat.md) -- document grounding with citations
- [Client Factory](wiki/factory.md) -- runtime provider selection for multi-tenant / dynamic scenarios
- [Feature Extensions](wiki/feature-extensions.md) -- Feature Collection pattern

## Samples

- Interactive console demo: [src/Cisharp.Console/](src/Cisharp.Console/) -- covers all providers and features
- Logging: every HTTP call emits structured `ILogger` entries (EventIds 1000–1005). Wire any sink via `ILoggerFactory` -- see [wiki/logging.md](wiki/logging.md).

## HTTP Resilience

The provider DI helpers (`AddOpenAiClient`, `AddAzureOpenAiClient`, `AddAzureAiInferenceChatCompletion`, `AddAnthropicClient`, `AddCohereChatClient`, and embedding equivalents) automatically add `AddCisharpaiResilienceHandler()` to their `HttpClient` registrations.

`AddCisharpaiResilienceHandler()` uses `Microsoft.Extensions.Http.Resilience` / Polly standard HTTP resilience with:

- Retries for transient failures, including HTTP `408`, `429`, `5xx`, `HttpRequestException`, and timeout failures
- `Retry-After` header support for retry delays, including rate-limit responses such as `429 Too Many Requests`
- 3 retry attempts, 500 ms initial delay, exponential backoff, and jitter
- 60 second per-attempt timeout and 90 second total request timeout
- Circuit breaker with 120 second sampling, 20% failure ratio, minimum 10 requests, and 15 second break duration

If all retries fail, provider clients return `IsSuccess = false` and `ErrorMessage` for API errors instead of throwing. Network/configuration failures may still throw.

The static `Create(...)` factory methods do not add DI resilience by themselves. When using `Create(...)`, register the named handler with resilience at startup:

```csharp
services.AddHttpClient("cisharpai")
    .AddCisharpaiResilienceHandler();
```

Streaming calls can run longer than the standard 60s/90s timeouts. For long-running streams, configure a streaming-specific HTTP client with `AddCisharpaiStreamingResilienceHandler()`, which removes those timeouts while keeping retry and circuit-breaker policies.

## Building Locally

The project includes a PowerShell build script that handles versioning, building, testing, and NuGet packaging.

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PowerShell 7+](https://github.com/PowerShell/PowerShell) (`pwsh`)

### Running the build

```bash
pwsh scripts/build.ps1
```

This will:
1. Restore dotnet tools (including [GitVersion](https://gitversion.net/))
2. Calculate the version using GitVersion (ContinuousDeployment mode)
3. Restore NuGet packages
4. Build the solution in Release configuration
5. Run unit tests on both net8.0 and net10.0
6. Pack NuGet packages into `artifacts/NuGet/`

### Build options

```bash
# Skip tests
pwsh scripts/build.ps1 -skiptest

# Build and publish to nuget.org
pwsh scripts/build.ps1 -nugetApiKey "YOUR_KEY" -nugetPublish $true
```

### Build artifacts

| Output | Location |
|--------|----------|
| NuGet packages (`.nupkg` + `.snupkg`) | `artifacts/NuGet/` |
| Test results (`.trx`) | `artifacts/TestResults/` |

## Running Integration Tests

Integration tests require environment variables to be set. Create a `.env` file in the repository root or set them in your environment.

### Environment Variables Format

| Variable | Format | Example |
|----------|--------|---------|
| `OPENAI_TEST_API_KEY` | OpenAI API key | `sk-proj-...` |
| `ANTHROPIC_TEST_API_KEY` | Anthropic API key | `sk-ant-...` |
| `AZURE_OPENAI_TEST_ENDPOINT` | Azure OpenAI endpoint URL (no trailing slash) | `https://myresource.openai.azure.com` |
| `AZURE_OPENAI_TEST_API_KEY` | Azure OpenAI API key | `abc123...` |
| `AZURE_OPENAI_TEST_DEPLOYMENTS` | Comma-separated deployment names | `gpt-4o,gpt-4o-mini` |
| `AZURE_OPENAI_TEST_EMBEDDING_DEPLOYMENT` | Single embedding deployment name | `text-embedding-ada-002` |
| `AZURE_INFERENCE_TEST_ENDPOINT` | Azure AI Inference endpoint URL | `https://mymodel.eastus.models.ai.azure.com` |
| `AZURE_INFERENCE_TEST_API_KEY` | Azure AI Inference API key | `abc123...` |
| `AZURE_INFERENCE_TEST_MODELS` | Comma-separated model IDs for Azure AI Inference | `Phi-3-mini-4k-instruct` |
| `AZURE_INFERENCE_TEST_EMBEDDING_ENDPOINT` | Azure AI Inference embedding endpoint URL | `https://myembedding.eastus.models.ai.azure.com` |
| `AZURE_INFERENCE_TEST_EMBEDDING_KEY` | Azure AI Inference embedding API key | `abc123...` |
| `AZURE_INFERENCE_TEST_EMBEDDING_MODEL` | Model ID for Azure AI Inference embedding | `Cohere-embed-v3-english` |
| `COHERE_TEST_API_KEY` | Cohere API key | `...` |

### Example `.env` file

```
# OpenAI
OPENAI_TEST_API_KEY=sk-proj-your-key-here

# Azure OpenAI
AZURE_OPENAI_TEST_ENDPOINT=https://myresource.openai.azure.com
AZURE_OPENAI_TEST_API_KEY=your-azure-openai-key
AZURE_OPENAI_TEST_DEPLOYMENTS=gpt-4o,gpt-4o-mini
AZURE_OPENAI_TEST_EMBEDDING_DEPLOYMENT=text-embedding-ada-002

# Azure AI Inference
AZURE_INFERENCE_TEST_ENDPOINT=https://mymodel.eastus.models.ai.azure.com
AZURE_INFERENCE_TEST_API_KEY=your-azure-inference-key
AZURE_INFERENCE_TEST_MODELS=Phi-3-mini-4k-instruct
AZURE_INFERENCE_TEST_EMBEDDING_ENDPOINT=https://myembedding.eastus.models.ai.azure.com
AZURE_INFERENCE_TEST_EMBEDDING_KEY=your-azure-inference-embedding-key
AZURE_INFERENCE_TEST_EMBEDDING_MODEL=Cohere-embed-v3-english

# Anthropic
ANTHROPIC_TEST_API_KEY=sk-ant-your-key-here

# Cohere
COHERE_TEST_API_KEY=your-cohere-key
```

**Note:** The `.env` file parser supports both quoted and unquoted values, and lines starting with `#` are treated as comments.

## License

See the repository license file for terms.
