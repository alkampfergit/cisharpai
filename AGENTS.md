# Cisharpai

A unified .NET client library providing a common HttpClient-based interface for multiple LLM providers (OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere).

## Key Principles

- **Unified Abstraction**: Same interface for all providers — switching is a configuration change.
- **"Escape Hatch" Extensibility**: `ExtraParameters` deep-merges arbitrary JSON into requests for bleeding-edge features.
- **No Exceptions for API Errors**: Clients return `IsSuccess=false` + `ErrorMessage` instead of throwing. Exceptions only for network/config issues.
- **Debuggability First**: `RawResponseJson`/`RawRequestJson` on all responses.
- **Immutability**: Request/Response DTOs are immutable records.
- **Feature Collection Pattern**: Optional capabilities discovered via `IHasFeatures.Features.Get<T>()` (like ASP.NET Core `HttpContext.Features`).

## Security Rules

- NEVER read, cat, or display .env files. These contain secrets and API keys that must not be exposed.

## General Rules

- Claude skills can be shared with Codex by running `python3 .agents/link_claude_skills.py`.
  Read the relevant `.agents/skills/<name>/SKILL.md` before use. See
  `.agents/README.md` for setup and `.agents/claude-skills-summary.md` for compatibility notes.

- Source code is in `src/` folder, both projects and tests.
- Projects multitarget .NET 8.0 and .NET 10.
- Single test project multitargets .NET 8.0 and .NET 10.
- Write tests for every functionality you add. Do not consider task finished if tests are not green.
- If you need to mock, use NSubstitute.
- Keep the wiki up to date after you generate new features.
- Keep [RELEASE_NOTES.md](RELEASE_NOTES.md) up to date with one line for each new user-facing feature.
- After modifying code, update [project_overview.md](memories/project_overview.md) if needed.

## Project Structure

Detailed structure: [project_overview.md](memories/project_overview.md)

### Source Projects

| Project | Description |
|---------|-------------|
| `src/Cisharpai/` | Core abstractions: `IChatCompletionClient`, `IEmbeddingClient`, Feature interfaces, Models, Helpers |
| `src/Cisharpai.OpenAi/` | OpenAI provider (Chat Completions API, Responses API, embeddings) |
| `src/Cisharpai.Azure/` | Azure OpenAI + Azure AI Inference providers |
| `src/Cisharpai.Anthropic/` | Anthropic (Claude) provider |
| `src/Cisharpai.Cohere/` | Cohere provider (chat, embeddings v3/v4, grounded chat) |
| `src/Cisharpai.Testing/` | Fake clients for unit testing (`FakeChatCompletionClient`, `FakeEmbeddingClient`, `FakeResponses`) |
| `src/Cisharp.Console/` | Interactive demo app with Spectre.Console menu |

### Feature Interfaces (all in `src/Cisharpai/Features/`)

| Feature | Providers |
|---------|-----------|
| `IJsonOutputFeature` | All 5 chat clients |
| `IToolCallingFeature` | All 5 chat clients |
| `IStreamingChatFeature` | All 5 chat clients |
| `IGroundedChatFeature` | Cohere only |
| `IImageEmbeddingFeature` | Azure AI Inference, Cohere |
| `IMultimodalEmbeddingFeature` | Cohere only |

### Test Projects

| Project | Description |
|---------|-------------|
| `src/Cisharpai.Tests/` | Unit tests (all providers, models, features, DI, fakes) |
| `src/Cisharpai.Tests.Common/` | Shared test utilities (`DotEnvLoader`, `TestEnvironmentVariables`) |
| `src/Cisharpai.Integration.Tests/` | Integration tests against real APIs (compiled only for .NET 10) |

### Build & CI

- `scripts/build.ps1` — Build, test, pack (6 NuGet packages). Outputs to `artifacts/`.
- `.github/workflows/ci.yml` — Main CI: build + unit tests + integration tests.
- `GitVersion.yml` — ContinuousDeployment mode. Labels: `alpha` (develop/feature), `beta` (release/hotfix).

## Post-Change Checklist

When modifying core features (new feature interface, new provider, new model support, changed API):
1. Update `wiki/` — relevant feature guide(s), `wiki/provider-features.md` matrix, `wiki/index.md` TOC if new page
2. Update `wiki/testing.md` — if new feature interfaces are added, document how to fake them
3. Update `memories/project_overview.md` — reflect structural changes
4. Update `Cisharpai.Testing` — add queues/defaults/capture for any new feature interface on fake clients
5. Update `scripts/build.ps1` `$packProjects` if a new publishable project is added
6. Update `RELEASE_NOTES.md` — add one line for each new user-facing feature
7. Update if needed the [integrated skill](./llm/cisharpai-expert/SKILL.md) to include all the details on how user of library can use the various features.

## Environment Variable Maintenance

When adding/modifying env vars for integration tests, update:
1. `src/Cisharpai.Tests.Common/TestEnvironmentVariables.cs`
2. `src/Cisharpai.Integration.Tests/DotEnv.cs`
3. `src/Cisharpai.Integration.Tests/EnvironmentConfigurationTests.cs`
4. `scripts/gh-secrets-from-dotenv.zsh`
5. `memories/project_overview.md`

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
