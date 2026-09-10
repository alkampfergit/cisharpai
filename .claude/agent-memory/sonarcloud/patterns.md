# SonarCloud Issue Patterns

Last updated: 2026-05-06

## Rules encountered

### S5852 - Regex vulnerable to super-linear runtime
- **File**: `src/commands/get-item.ts`
- **Fix**: Replaced the fallback HTML tag stripping regex with a linear character
  scan helper to avoid backtracking on malformed tag input while preserving
  existing output
- **Issue**: `azdo-cli-4sl`

### S6551 - Avoid String() on potentially object-typed value
- **File**: Multiple command files
- **Fix**: Use explicit type checks or template literals instead of `String(value)`
- **Commit**: 6229d65

### shelldre:S7688 - Use [[ ]] instead of [ ] in bash conditionals
- **Language**: Shell (bash)
- **Files**: `.devcontainer/postcreate.sh`, `.devcontainer/setup-git-aliases.sh`
- **Fix**: Replace every `if [ ... ]` and `elif [ ... ]` with `if [[ ... ]]`/`elif [[ ... ]]`.
  Combined `||`-joined single-bracket tests into a single `[[ cond1 || cond2 ]]`.
- **Why**: `[[` avoids word splitting/glob expansion on variables; bash-idiomatic.
  SonarCloud flags these as RELIABILITY/HIGH.
- **PR**: alkampfergit/cisharpai#21 (2026-05-06)

### external_roslyn:NUnit2045 - Wrap independent asserts in Assert.Multiple
- **Language**: C#/.NET (NUnit)
- **Fix**: Suppress via `.editorconfig` — not enforced in this project.
  Add to `.editorconfig` scoped to `[**/*Tests.cs]`:
  `dotnet_diagnostic.NUnit2045.severity = none`
- **Why suppress**: The rule fires on every independent assert pair but using
  Assert.Multiple everywhere adds noise with no real benefit in simple tests.
  Suppressing at the Roslyn level makes SonarCloud (which ingests Roslyn diagnostics)
  stop reporting it too.
- **PR**: alkampfergit/cisharpai#22 (2026-05-06)

### external_roslyn:CA2016 - Forward CancellationToken to async calls
- **Language**: C#/.NET
- **Fix**: Change `ReadAsStringAsync()` → `ReadAsStringAsync(CancellationToken.None)`
  in test HTTP handler lambdas. Global sed works well:
  `find src/ -name "*.cs" | xargs sed -i 's/ReadAsStringAsync()/ReadAsStringAsync(CancellationToken.None)/g'`
- **Why**: Test lambdas use `CancellationToken _` (discard); the analyzer requires
  either forwarding the token or passing `CancellationToken.None` explicitly.
  `CancellationToken.None` is correct for test stubs — they are not real operations.
- **PR**: alkampfergit/cisharpai#22 (2026-05-06)

### Duplication - Repeated command boilerplate
- **Files**: assign.ts, get-item.ts, set-field.ts, set-state.ts
- **Pattern**: Each command had identical `resolveContext()`, `parseWorkItemId()`,
  `validateOrgProjectPair()`, and `handleCommandError()` implementations
- **Fix**: Extracted to `src/services/command-helpers.ts` and `src/services/context.ts`
- **Commits**: 78269b4, e54696f

### Duplication - Repeated test patterns
- **Files**: azdo-client.test.ts, html-detect.test.ts, multiple test files
- **Pattern**: Identical mock setups and assertion patterns repeated across tests
- **Fix**: Extracted to `tests/unit/helpers/api-test-utils.ts` and
  `tests/unit/helpers/command-test-utils.ts`, converted to `it.each` patterns
- **Commits**: eed2ee9, 9716df4

## Conflict hotspots

Files that cause merge conflicts when modified by parallel agents:
- `src/index.ts` (9 modifications across branches)
- `src/services/azdo-client.ts` (8 modifications)
- `package.json` (8 modifications)
- `tests/unit/azdo-client.test.ts` (4 modifications)

## Lessons learned

1. **Never use parallel agents for duplication fixes** -- they are inherently
   cross-cutting and touch overlapping files
2. **SonarCloud duplication threshold** applies to new code on the PR diff,
   not the whole codebase
3. **Extracting shared helpers** can itself cause duplication issues if the
   helper is too similar to existing code -- check for existing utilities first
