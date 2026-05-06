---
name: sonarcloud
description: >
  Analyze and fix SonarCloud issues on pull requests. Use when the user mentions
  SonarCloud, quality gate, code smells, duplication, or wants to check PR quality
  status. Reads issues via GitHub API, creates fix plans, and applies fixes with
  awareness of parallel agent safety.
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

## Project Discovery

Before querying SonarCloud, find the project key and organization:

```bash
# From CI workflow (most reliable)
grep -r "sonarscanner\|sonar.projectKey\|/k:" .github/workflows/

# The key pattern is always: {org}_{repo} or defined explicitly
# Example: /k:"alkampfergit_cisharpai" /o:"alkampfergit-github"
```

The SonarCloud API base URL is `https://sonarcloud.io`. No authentication is
needed for public projects.

---

## Read PR Issues

### Step 1 -- Find the PR

```bash
# List open PRs
gh pr list --state open

# Get PR checks status
gh pr checks <pr-number>
```

### Step 2 -- Read SonarCloud results

**Primary method: SonarCloud public API** (most reliable, returns structured JSON):

```bash
# Get all issues for a PR -- no authentication needed for public projects
curl -s "https://sonarcloud.io/api/issues/search?componentKeys={owner}_{repo}&pullRequest={pr-number}&statuses=OPEN,CONFIRMED&ps=50"

# Get issues for the main branch (no PR filter)
curl -s "https://sonarcloud.io/api/issues/search?componentKeys={owner}_{repo}&statuses=OPEN,CONFIRMED&impactSeverities=HIGH&ps=50"

# Use facets to get rule distribution across all pages efficiently
curl -s "https://sonarcloud.io/api/issues/search?componentKeys={owner}_{repo}&statuses=OPEN,CONFIRMED&impactSeverities=MEDIUM&ps=1&facets=rules"
```

**Fallback: GitHub check runs** (summary only, no individual issue details):

```bash
# IMPORTANT: The app slug is "sonarqubecloud" (NOT "sonarcloud")
gh api repos/{owner}/{repo}/commits/{sha}/check-runs \
  --jq '.check_runs[] | select(.app.slug == "sonarqubecloud") | {name, conclusion, summary: .output.summary}'
```

**Note**: The SonarCloud bot comment may not always be present on PRs. The check run summary and the direct API are more reliable.

### Step 3 -- Parse the results

From the SonarCloud API response, extract for each issue:
- **rule** -- the SonarCloud rule ID (e.g., `csharpsquid:S3776`, `external_roslyn:CA2016`)
- **severity** -- MINOR, MAJOR, CRITICAL
- **message** -- human-readable description
- **component** -- file path (strip the `{project_key}:` prefix)
- **line** -- line number
- **type** -- BUG, VULNERABILITY, CODE_SMELL
- **impacts** -- softwareQuality + severity (e.g., MAINTAINABILITY/HIGH)

Use the `facets=rules` parameter to get a count-per-rule summary efficiently
instead of fetching all pages:

```python
# Parse rule distribution
data = json.load(sys.stdin)
facets = {f['property']: f['values'] for f in data.get('facets', [])}
for r in sorted(facets['rules'], key=lambda x: -x['count'])[:10]:
    print(f"{r['val']} -> {r['count']}")
```

Present a concise summary table to the user grouped by rule.

---

## Fix Issues Workflow

### Step 1 -- Categorize issues

Group SonarCloud issues by type:
1. **Bugs** -- Fix first (highest impact)
2. **Vulnerabilities** -- Fix second
3. **Code smells** -- Fix third
4. **Duplication** -- Fix last (see [Fix Duplication](#fix-duplication))

Within each type, fix by **count descending** — the highest-count rule clears
the most issues per unit of effort.

### Step 2 -- Plan fixes with parallel safety

**CRITICAL**: Check memory for known conflict hotspots before assigning work.

When using parallel agents to fix issues:
- **Partition by file** -- each agent gets exclusive files, never overlapping
- **Never modify shared hub files** in parallel (e.g., `src/index.ts`, `package.json`, `Directory.Build.props`)
- **Cross-cutting refactors** (extracting shared helpers, renaming across files) must be done sequentially, not in parallel

Safe parallel pattern:
```
Agent 1: fixes in src/commands/get-md-field.ts + tests/unit/get-md-field.test.ts
Agent 2: fixes in src/commands/set-md-field.ts + tests/unit/set-md-field.test.ts
Agent 3: fixes in src/services/html-detect.ts + tests/unit/html-detect.test.ts
```

Unsafe parallel pattern (AVOID):
```
Agent 1: extracts shared helper from get-item.ts, set-field.ts, assign.ts
Agent 2: fixes duplication in get-item.ts, set-state.ts
-- CONFLICT: both touch get-item.ts
```

### Step 3 -- Apply fixes

For each issue:
1. Read the affected file
2. Understand the SonarCloud rule being violated (see [Known Rules](#known-rules) below)
3. Apply the minimal fix (don't over-refactor)
4. Verify the build is clean

### Step 4 -- Verify

Choose the right check command for the project's tech stack:

```bash
# .NET projects
dotnet build --nologo && dotnet test --no-build

# Node.js / TypeScript projects
npm test && npm run lint && npm run typecheck

# General: always confirm 0 errors and all tests pass before committing
```

Then commit and push:

```bash
git add <specific-files>
git commit -m "Fix SonarCloud issues: <brief description>"
git push
```

After pushing, the SonarCloud check will re-run automatically on the PR.

### Step 5 -- Update memory

After fixing issues, update `memory/patterns.md` with:
- Which rules triggered and how they were fixed
- Language/ecosystem and why the fix is correct
- Any new hotspot files identified
- PR reference and date

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
curl -s "https://sonarcloud.io/api/issues/search?componentKeys={owner}_{repo}&pullRequest={pr-number}&statuses=OPEN,CONFIRMED&ps=50"

# Or via GitHub check runs (summary only, slug is "sonarqubecloud")
gh api repos/{owner}/{repo}/commits/{sha}/check-runs \
  --jq '.check_runs[] | select(.app.slug == "sonarqubecloud") | .output.summary'
```

---

## Suppression Strategies

Sometimes a rule genuinely doesn't apply to the project. Suppress at the source
(build level) so SonarCloud stops seeing the diagnostic — never suppress only
in SonarCloud's UI, as that doesn't help other developers.

### .NET / C# — suppress a Roslyn analyzer rule

```ini
# .editorconfig at repo root
root = true

# Scope to test files only if appropriate
[**/*Tests.cs]
dotnet_diagnostic.NUnit2045.severity = none
dotnet_diagnostic.CA2016.severity = none
```

After adding `.editorconfig`, rebuild — the warning count should drop.
SonarCloud picks up the suppression on next analysis because it runs
the same Roslyn analyzers.

### TypeScript / ESLint — suppress a rule project-wide

```json
// .eslintrc.json
{ "rules": { "rule-name": "off" } }
```

Or inline for a specific line:
```ts
// eslint-disable-next-line rule-name
```

---

## Fix Duplication

Duplication is a common quality gate failure.

### Detection

SonarCloud flags duplicated blocks (usually 10+ lines of identical or near-identical code).

### Common duplication patterns

1. **Command boilerplate** -- ID parsing, validation, error handling repeated across command files
   - **Fix**: Extract to shared helpers

2. **Test setup** -- Same mock setup repeated across test files
   - **Fix**: Extract to shared test utilities; use parameterised tests (`it.each` / `[TestCase]`)

3. **API call patterns** -- Similar fetch/auth/error patterns
   - **Fix**: Extract shared client/helper

### Duplication fix strategy

1. Identify the duplicated blocks from SonarCloud report
2. Find all instances in the codebase using Grep
3. Extract shared code into a helper/utility
4. Replace all instances with calls to the shared code
5. Ensure tests still pass

**Important**: Duplication fixes are cross-cutting refactors. Do them
**sequentially**, not in parallel agents. See [parallel safety](#step-2----plan-fixes-with-parallel-safety).

---

## Known Rules

Rules encountered across projects, with proven fixes.

### Shell / Bash

| Rule | What it means | Fix |
|---|---|---|
| `shelldre:S7688` | Use `[[` instead of `[` for conditional tests | Replace all `if [ ... ]` and `elif [ ... ]` with `if [[ ... ]]`. Merge `[ a ] \|\| [ b ]` into `[[ a \|\| b ]]`. Severity: RELIABILITY/HIGH |

### C# / .NET

| Rule | What it means | Fix |
|---|---|---|
| `external_roslyn:CA2016` | Forward `CancellationToken` to async methods | In test HTTP handler lambdas: `ReadAsStringAsync()` → `ReadAsStringAsync(CancellationToken.None)`. Use sed for bulk fix across test files |
| `external_roslyn:NUnit2045` | Wrap independent asserts in `Assert.Multiple` | Suppress via `.editorconfig`: `dotnet_diagnostic.NUnit2045.severity = none` scoped to `[**/*Tests.cs]` if not enforced |
| `external_roslyn:NUnit2046` | Use `Has.Length.EqualTo(n)` instead of `Is.EqualTo(n)` on `.Length` | Change `Assert.That(col.Length, Is.EqualTo(n))` → `Assert.That(col, Has.Length.EqualTo(n))` |
| `external_roslyn:NUnit2009` | Actual and expected arguments are the same | Swap arguments so actual comes first, or fix the assertion logic |
| `external_roslyn:NUnit4002` | Replace `Is.EqualTo(true)` with `Is.True` | Direct substitution |
| `external_roslyn:CA1822` | Method doesn't access instance data | Add `static` modifier |
| `csharpsquid:S6966` | Awaitable method called without `await` | Add `await` keyword |
| `csharpsquid:S4457` | Method mixes sync validation and async code | Split into a sync validation method + separate async core |

### TypeScript / JavaScript

| Rule | What it means | Fix |
|---|---|---|
| `S1192` | String literal duplication | Extract to constant |
| `S3358` | Nested ternary operations | Extract to if/else or helper function |
| `S3776` | Cognitive complexity too high | Extract helper functions |
| `S1481` | Unused local variable | Remove it |
| `S5852` | Regex vulnerable to super-linear runtime | Replace backtracking regex with a linear character scan |
| `S6551` | Avoid `String()` on object types | Use explicit `.toString()` or template literal |
| `S6606` | Ternary instead of nullish coalescing | Use `??` operator |
| `S7735` | Unexpected negated condition | Flip condition |
| `S7780` | Backslash escaping in strings | Use `String.raw` tagged template |
| `typescript:S107` | Too many parameters | Use options object |
| Duplication | Code blocks repeated | Extract shared helper |

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

**RTK proxy summarizes curl output (token-saving mode)**
- Use `rtk proxy curl ...` to bypass filtering and get raw JSON for parsing
- Pipe to `python3 -c "import json,sys; ..."` for structured extraction

**`impactSeverities` vs `severities` parameter**
- Use `impactSeverities=HIGH` (new Clean Code model) not `severities=CRITICAL`
- The new model maps to RELIABILITY/MAINTAINABILITY/SECURITY qualities with HIGH/MEDIUM/LOW severity
