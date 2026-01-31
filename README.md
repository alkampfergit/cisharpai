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

## CI/CD & Releases

This project uses GitHub Actions for continuous integration and NuGet publishing. Versioning is handled automatically by [GitVersion](https://gitversion.net/) (GitFlow workflow).

### How it works

- **Every push** to `main`, `master`, `develop`, or `feature/**` branches triggers build + unit tests + pack.
- **Pull requests** to `main`/`develop` trigger build + unit tests.
- **Integration tests** run on push and workflow_dispatch events (requires API key secrets).
- **NuGet publishing** happens only when a `v*` tag is pushed.

### Creating a release

```bash
# 1. Ensure develop is up to date
git checkout develop && git pull

# 2. Merge to master
git checkout master && git merge develop

# 3. Tag the release (GitVersion determines the version)
git tag v1.0.0

# 4. Push the tag to trigger publish
git push origin master --tags
```

The pipeline will build, test, pack, and push all NuGet packages (with `.snupkg` symbol packages) to nuget.org.

### Required secrets

| Secret | Description |
|--------|-------------|
| `NUGET_API_KEY` | NuGet.org API key for publishing |
| `OPENAI_TEST_API_KEY` | OpenAI API key (integration tests) |
| `ANTHROPIC_TEST_API_KEY` | Anthropic API key (integration tests) |
| `AZURE_OPENAI_TEST_ENDPOINT` | Azure OpenAI endpoint URL (integration tests) |
| `AZURE_OPENAI_TEST_API_KEY` | Azure OpenAI API key (integration tests) |
| `AZURE_OPENAI_TEST_DEPLOYMENTS` | Comma-separated Azure deployment names (integration tests) |
| `COHERE_TEST_API_KEY` | Cohere API key (integration tests) |

Use `scripts/gh-secrets-from-dotenv.zsh` to set test secrets from a local `.env` file.

## License

See the repository license file for terms.
