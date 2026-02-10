# Beads Fixer Agent Memory

## Project Quick Reference
- Source: `src/Cisharpai/` (core), provider projects alongside
- Tests: `src/Cisharpai.Tests/` (unit), `src/Cisharpai.Integration.Tests/` (integration)
- Multi-target: net8.0 + net10.0
- Test framework: NUnit with NSubstitute for mocking

## Common Bug Patterns

### Missing Cascaded Validation (cisharpai-7b5)
- Parent `Validate()` methods may check their own fields but forget to call `Validate()` on nested child objects
- Example: `GroundedChatOptions.Validate()` checked Documents list but never called `DocumentChunk.Validate()` on each item
- Fix pattern: Add `foreach` loop calling child `.Validate()` after parent validation
- Always check: When a model has a `Validate()` method and is composed into another model, does the parent cascade?

### Per-Instance Allocation of Shared Resources (cisharpai-ae8)
- `JsonSerializerOptions` and similar expensive objects should be `static readonly` when default config is identical across instances
- `LlmHttpClient` was creating a new `JsonSerializerOptions` per instance; fixed with `DefaultSerializerOptions` static field
- Note: Cohere clients still create per-instance `JsonSerializerOptions` with `SnakeCaseLower` -- could be further optimized

## Testing Patterns
- Tests use NUnit `[Test]` attribute
- Validation tests: `Assert.Throws<ArgumentException>(() => obj.Validate())` for invalid, `Assert.DoesNotThrow(() => obj.Validate())` for valid
- Test both positive and negative cases, plus edge cases (mix of valid/invalid items in collections)
- Use reflection (`BindingFlags.NonPublic | BindingFlags.Instance`) to verify internal state sharing in tests
- Tests run on both net8.0 and net10.0 (384 tests total as of 2026-02-10)

## Build & Run Commands
- Build: `dotnet build src/Cisharpai/Cisharpai.csproj --configuration Release`
- Unit tests: `dotnet test src/Cisharpai.Tests/`
- Filtered tests: `dotnet test src/Cisharpai.Tests/ --filter "FullyQualifiedName~ClassName"`
- Full build script: `pwsh scripts/build.ps1`
