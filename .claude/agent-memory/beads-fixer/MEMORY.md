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

## Testing Patterns
- Tests use NUnit `[Test]` attribute
- Validation tests: `Assert.Throws<ArgumentException>(() => obj.Validate())` for invalid, `Assert.DoesNotThrow(() => obj.Validate())` for valid
- Test both positive and negative cases, plus edge cases (mix of valid/invalid items in collections)
- Tests run on both net8.0 and net10.0 (382 tests total as of 2026-02-10)

## Build & Run Commands
- Build: `dotnet build src/Cisharpai/Cisharpai.csproj --configuration Release`
- Unit tests: `dotnet test src/Cisharpai.Tests/`
- Filtered tests: `dotnet test src/Cisharpai.Tests/ --filter "FullyQualifiedName~ClassName"`
- Full build script: `pwsh scripts/build.ps1`
