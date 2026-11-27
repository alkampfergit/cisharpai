# SonarCloud Issue Patterns

Last updated: 2026-11-27

## Rules encountered

### S6966 - Use async overload
- **Files**: Multiple provider clients
- **Fix**: Replaced sync I/O calls with async equivalents + cancellation token
- **Commit**: 58c4bae

### S8969 - Logging template should not vary
- **Files**: Provider client files (Anthropic, Cohere, OpenAI)
- **Fix**: Replaced string-interpolated log calls with `[LoggerMessage]` source-generated
  partial methods in dedicated `*.Log.cs` files
- **Commit**: 58c4bae

### S108 - Empty block
- **Files**: Various
- **Fix**: Added comment explaining why block is intentionally empty, or removed dead code
- **Commit**: 58c4bae

### S4136 - Method overloads should be adjacent
- **Files**: Various
- **Fix**: Reordered methods so overloads are grouped together
- **Commit**: 58c4bae

## Conflict hotspots

Files that cause merge conflicts when modified by parallel agents:
- `Directory.Build.props` (shared build configuration)
- `Directory.Packages.props` (centralized package versions)
- Provider client files that implement multiple features

## Lessons learned

1. **Never use parallel agents for duplication fixes** -- they are inherently
   cross-cutting and touch overlapping files
2. **SonarCloud duplication threshold** applies to new code on the PR diff,
   not the whole codebase
3. **This project uses `TreatWarningsAsErrors`** -- SonarAnalyzer.CSharp warnings
   become build errors locally, so most issues are caught before pushing
4. **`[LoggerMessage]` pattern** -- this project uses partial class `*.Log.cs` files
   for source-generated logging; prefer this over inline `_logger.LogXxx()` calls
