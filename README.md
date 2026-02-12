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

## Documentation

Start here:

- [wiki/index.md](wiki/index.md)
- [wiki/getting-started.md](wiki/getting-started.md)
- [wiki/openai.md](wiki/openai.md)

## Samples

- OpenAI console scenario: [src/Cisharp.Console/Scenarios/OpenAiChatScenario.cs](src/Cisharp.Console/Scenarios/OpenAiChatScenario.cs)

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
