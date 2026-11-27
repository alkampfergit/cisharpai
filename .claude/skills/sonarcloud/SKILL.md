---
name: sonarcloud
description: >
  Analyze and fix SonarCloud issues on pull requests. Use when the user mentions
  SonarCloud, quality gate, code smells, duplication, or wants to check PR quality
  status. Reads issues via SonarCloud API, creates fix plans, and applies fixes.
---

# SonarCloud Quality Skill

This skill handles reading, analyzing, and fixing SonarCloud issues reported on
pull requests. SonarCloud runs as a GitHub App and reports results as PR checks
and comments.

## Memory

This skill maintains persistent memory in `memory/` within this skill directory.
After each session, update memory files with:
- New issue patterns encountered and how they were resolved
- Project-specific SonarCloud rules that frequently trigger
- Files that are chronic hotspots for issues

Before starting work, always read memory files to leverage past experience.

## When to use this skill

| User says... | Action |
|---|---|
| "check sonarcloud" / "quality gate" | [Read PR Issues](#read-pr-issues) |
| "fix sonarcloud issues" / "fix code smells" | [Fix Issues Workflow](#fix-issues-workflow) |
| "sonarcloud is failing" / "quality gate failing" | [Diagnose Quality Gate](#diagnose-quality-gate) |
| "check duplication" / "fix duplication" | [Fix Duplication](#fix-duplication) |

---

## Read PR Issues

### Step 1 -- Find the PR

```bash
gh pr list --state open
gh pr checks <pr-number>
```

### Step 2 -- Read SonarCloud results

**Primary method: SonarCloud public API** (most reliable, returns structured JSON):

```bash
# Get all issues for a PR -- no authentication needed for public projects
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=alkampfergit_cisharpai&pullRequest={pr-number}&statuses=OPEN,CONFIRMED&ps=50"
```

This returns structured JSON with full issue details: rule, severity, message, file, line number, effort.

**Fallback: GitHub check runs** (summary only, no individual issue details):

```bash
# IMPORTANT: The app slug is "sonarqubecloud" (NOT "sonarcloud")
gh api repos/{owner}/{repo}/commits/{sha}/check-runs \
  --jq '.check_runs[] | select(.app.slug == "sonarqubecloud") | {name, conclusion, summary: .output.summary}'
```

**Note**: The SonarCloud bot comment may not always be present on PRs. The check run summary and the direct API are more reliable.

### Step 3 -- Parse the results

From the SonarCloud API response, extract for each issue:
- **rule** -- the SonarCloud rule ID (e.g., `csharpsquid:S3776`)
- **severity** -- MINOR, MAJOR, CRITICAL
- **message** -- human-readable description
- **component** -- file path (strip the `{project_key}:` prefix)
- **line** -- line number
- **type** -- BUG, VULNERABILITY, CODE_SMELL
- **impacts** -- softwareQuality + severity (e.g., MAINTAINABILITY/HIGH)

Present a concise summary table to the user.

---

## Fix Issues Workflow

### Step 1 -- Categorize issues

Group SonarCloud issues by type:
1. **Bugs** -- Fix first (highest impact)
2. **Vulnerabilities** -- Fix second
3. **Code smells** -- Fix third
4. **Duplication** -- Fix last (see [Fix Duplication](#fix-duplication))

### Step 2 -- Plan fixes with parallel safety

**CRITICAL**: Check memory for known conflict hotspots before assigning work.

When using parallel agents to fix issues:
- **Partition by file** -- each agent gets exclusive files, never overlapping
- **Never modify shared hub files** in parallel (e.g., `Directory.Build.props`, `Directory.Packages.props`)
- **Cross-cutting refactors** (extracting shared helpers, renaming across files) must be done sequentially, not in parallel

### Step 3 -- Apply fixes

For each issue:
1. Read the affected file
2. Understand the SonarCloud rule being violated
3. Apply the minimal fix (don't over-refactor)
4. Run `dotnet build --verbosity quiet` and `dotnet test src/Cisharpai.Tests/Cisharpai.Tests.csproj --verbosity quiet` to verify

### Step 4 -- Verify

```bash
# Run full check suite
dotnet build --verbosity quiet
dotnet test src/Cisharpai.Tests/Cisharpai.Tests.csproj --verbosity quiet

# Commit and push
git add <specific-files>
git commit -m "fix(sonar): <brief description>"
git push
```

After pushing, the SonarCloud check will re-run automatically on the PR.

### Step 5 -- Update memory

After fixing issues, update `memory/patterns.md` with:
- Which rules triggered and how they were fixed
- Any new hotspot files identified

---

## Diagnose Quality Gate

When the quality gate fails, check these common causes:

1. **Coverage on new code < threshold** -- Need more tests
2. **Duplication on new code > threshold** -- See [Fix Duplication](#fix-duplication)
3. **New bugs/vulnerabilities** -- Must be zero for gate to pass
4. **New code smells above threshold** -- Reduce count

```bash
# Quick check: is the quality gate the only failing check?
gh pr checks <pr-number>

# Get detailed quality gate status via SonarCloud API (preferred)
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=alkampfergit_cisharpai&pullRequest={pr-number}&statuses=OPEN,CONFIRMED&ps=50"

# Or via GitHub check runs (summary only, slug is "sonarqubecloud")
gh api repos/{owner}/{repo}/commits/{sha}/check-runs \
  --jq '.check_runs[] | select(.app.slug == "sonarqubecloud") | .output.summary'
```

---

## Fix Duplication

Duplication is a common quality gate failure.

### Detection

SonarCloud flags duplicated blocks (usually 10+ lines of identical or near-identical code).

### Common duplication patterns in this project

1. **Provider boilerplate** -- Similar HTTP request/response handling repeated
   across provider clients (OpenAI, Anthropic, Azure, Cohere)
   - **Fix**: Extract to shared helpers in the core `Cisharpai` project

2. **Test setup** -- Same mock setup repeated across test files
   - **Fix**: Extract to shared test utilities or use `[SetUp]` methods

3. **Model mapping** -- Similar JSON deserialization patterns across providers
   - **Fix**: Use shared mapping helpers where the structure is identical

### Duplication fix strategy

1. Identify the duplicated blocks from SonarCloud report
2. Find all instances in the codebase
3. Extract shared code into a helper/utility
4. Replace all instances with calls to the shared code
5. Ensure tests still pass

**Important**: Duplication fixes are cross-cutting refactors. Do them
**sequentially**, not in parallel agents.

---

## Common SonarCloud Rules for C# Projects

| Rule | What it means | Typical fix |
|---|---|---|
| S1192 | String literal duplication | Extract to constant |
| S3776 | Cognitive complexity too high | Extract helper methods |
| S1481 | Unused local variable | Remove it |
| S1125 | Unnecessary boolean literal | Simplify condition |
| S3267 | Use LINQ instead of loop | Replace with LINQ expression |
| S6966 | Use async overload | Use `Async` method variant with `await` |
| S8969 | Logging template should not vary | Use `[LoggerMessage]` source generator |
| S108 | Empty block | Add comment or logic, or remove |
| S4136 | Method overloads should be adjacent | Reorder methods |
| S1172 | Unused parameter | Remove or prefix with `_` |
| S3928 | Parameter name in ArgumentException | Use `nameof()` |
| S2259 | Null dereference | Add null check or use `?.` |
| S4457 | Split parameter validation from async | Validate in sync wrapper, call private async impl |
| Duplication | Code blocks repeated | Extract shared helper |

## Mechanical (auto-applicable) fix recipes

### S6966 — Use async overload
```csharp
// Before:
stream.Write(buffer, 0, count);
// After:
await stream.WriteAsync(buffer, 0, count, cancellationToken);
```

### S8969 — Logging template varies at call site
```csharp
// Before:
_logger.LogWarning($"Skipping {type} citation");
// After: use [LoggerMessage] source generator in a partial class
[LoggerMessage(Level = LogLevel.Warning, Message = "Skipping {Type} citation")]
private partial void LogSkippingCitation(string type);
```

### S3776 — Reduce cognitive complexity
Extract nested logic into named private helpers:
1. Identify the innermost high-complexity block.
2. Extract it into a private method with a descriptive name.
3. The outer method calls the helper — complexity drops.
4. Repeat until the complexity score is ≤ 15.

### S1192 — String literal duplication
```csharp
// Before (same string in 3+ places):
if (type == "text") { ... }
// After:
private const string TextType = "text";
if (type == TextType) { ... }
```

---

## Troubleshooting

**SonarCloud check not appearing on PR**
- SonarCloud GitHub App may need to be re-authorized
- Check if the PR targets a branch SonarCloud is configured to analyze

**Quality gate passes locally but fails on SonarCloud**
- SonarCloud analyzes the diff against the target branch, not the full codebase
- Coverage and duplication thresholds apply only to new/changed code

**SonarCloud reports issues in files you didn't change**
- This can happen with duplication -- if you copied code from an existing file,
  both the source and destination are flagged
- Fix: refactor the shared code into a common location
