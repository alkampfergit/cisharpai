# Beads Implementer Memory

## Build & Test Commands
- Solution file: `/workspaces/cisharpai/Cisharpai.sln` (at repo root, NOT in src/)
- Build: `dotnet build /workspaces/cisharpai/Cisharpai.sln --configuration Release`
- Unit tests: `dotnet test /workspaces/cisharpai/src/Cisharpai.Tests/ --configuration Release --no-build`
- Integration tests compile only for .NET 10 (by convention)

## Project Patterns
- Feature Collection Pattern: clients implement feature interfaces and register `this` in constructor
- ToolChoice uses sealed abstract record hierarchy (not enum): Auto, None, Required, Specific(Name)
- All DTOs are immutable records; provider models use mutable classes with init setters
- Error handling: never throw for API errors, return `IsSuccess=false` responses
- ScenarioRegistry uses reflection to auto-discover IScenario implementations
- DI extensions own their options via closure (not registered in DI container)

## Provider-Specific Patterns
- OpenAI: tool_choice maps to string/object, arguments are JSON strings needing parse
- Anthropic: tool_result as user role with content blocks, tool_use as assistant content blocks, Content is `object` (string or List<ContentBlock>), ToolChoice.Required->any, Specific->{type:tool,name}
- Cohere: uppercase ToolChoice strings (AUTO/NONE/REQUIRED), Specific degrades to REQUIRED, strict_tools flag, snake_case serialization
- Azure OpenAI: identical JSON shape to OpenAI for tool calling, deployment-based routing, reasoning model detection
- Azure AI Inference: identical JSON shape to OpenAI for tool calling, model ID in request body, model-dependent tool support

## Test Patterns
- MockHttpMessageHandler captures request body for assertion
- JSON fixtures as const strings in test class
- Feature discovery tests verify `client.Features.Get<T>()` returns non-null and same instance
- Integration tests use `[TestCaseSource]` with env-var-driven model/deployment lists
- Azure AI Inference integration tests use `Assert.Inconclusive` for unsupported models (not Assert.Warn)
- Azure AI Inference tool-calling tests use `OneTimeTearDown` to verify at least one model passed strict assertions
- Use `Interlocked.Increment` for thread-safe success counters in parameterized integration tests

## Documentation Files to Update When Adding Features
1. `wiki/provider-features.md` - Feature matrix + provider details
2. `memories/project_overview.md` - Full project structure reference
3. Consider adding `wiki/<feature>.md` for user-facing docs
4. `src/Cisharpai.Tests/Features/FeatureDiscoveryTests.cs` - Feature discovery tests

## Environment Variable Maintenance (when adding new env vars)
1. `src/Cisharpai.Tests.Common/TestEnvironmentVariables.cs` - Add the constant
2. `src/Cisharpai.Integration.Tests/DotEnv.cs` - Re-export constant
3. `src/Cisharpai.Integration.Tests/EnvironmentConfigurationTests.cs` - Add to validation array
4. `scripts/gh-secrets-from-dotenv.zsh` - Add to allowlist array
5. `.envsample` - Add sample value
6. `README.md` - Update env var table and example .env

## Key Implementation Notes
- When adding tool calling to providers: Content property on message DTOs must be `string?` (nullable)
- Azure OpenAI/AI Inference tool calling uses the same JSON shape as standard OpenAI
- Both Azure providers support reasoning model detection for proper request format selection
- `bd sync` should be run after `bd close` to persist state
- Azure AI Inference model-as-a-service: each model gets its own endpoint, so embedding and chat may need separate endpoints/keys
- `src/Cisharpai.AzureOpenAi/` was a stale directory with only bin/obj (removed); the real code lives in `src/Cisharpai.Azure/`
- The actual NuGet package is `Cisharpai.Azure` (not `Cisharpai.AzureOpenAi`)
