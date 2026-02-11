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

## Test Patterns
- MockHttpMessageHandler captures request body for assertion
- JSON fixtures as const strings in test class
- Feature discovery tests verify `client.Features.Get<T>()` returns non-null and same instance

## Documentation Files to Update When Adding Features
1. `wiki/provider-features.md` - Feature matrix + provider details
2. `memories/project_overview.md` - Full project structure reference
3. Consider adding `wiki/<feature>.md` for user-facing docs
