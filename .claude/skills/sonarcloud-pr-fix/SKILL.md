---
name: sonarcloud-pr-fix
description: Use when the user wants to find SonarCloud issues for the current branch pull request, fix them in the local repo, verify with local checks, push the branch, and confirm whether SonarCloud has reanalyzed the PR.
---

# SonarCloud PR Fix

Use this skill for PR-scoped SonarCloud cleanup work in this repository.

## Goal

Find the SonarCloud issues attached to the current branch's pull request, apply the smallest safe fixes, verify locally, push the branch, and check whether SonarCloud has refreshed on the latest commit.

## Default assumptions

- Repo root is the current working directory.
- The branch usually has an open GitHub pull request.
- The SonarCloud project key is `alkampfergit_cisharpai`.
- Build uses `dotnet build` and tests use `dotnet test`.
- `TreatWarningsAsErrors` is enabled, so many Sonar rules are caught at build time.

## Workflow

1. Recover repo and branch context.
   - Read `AGENTS.md` and `CLAUDE.md`.
   - Run `git status --short --branch`.
   - Run `git rev-parse --abbrev-ref HEAD`.

2. Resolve the current branch to a GitHub pull request number.
   - Preferred:
     - `gh pr view --json number --jq .number`
   - If there is no open PR, stop and tell the user.

3. Query SonarCloud for PR issues AND security hotspots (two separate APIs).
   - Regular issues (bugs, vulnerabilities, code smells):
     - `curl -fsSL "https://sonarcloud.io/api/issues/search?componentKeys=alkampfergit_cisharpai&pullRequest=<prNumber>&ps=100"`
   - Security hotspots (NOT returned by issues/search — requires a separate call):
     - `curl -fsSL "https://sonarcloud.io/api/hotspots/search?projectKey=alkampfergit_cisharpai&pullRequest=<prNumber>&ps=100"`
   - Also check PR status when needed:
     - `curl -fsSL "https://sonarcloud.io/api/project_pull_requests/list?project=alkampfergit_cisharpai"`
   - Summarize findings by file, rule, and line before editing.
   - For hotspots, the fix strategy is to remove or isolate the risky pattern —
     do NOT mark as "reviewed/safe" unless the code genuinely has no risk.

4. Inspect the affected code and nearby tests.
   - Read only the files touched by SonarCloud plus relevant tests.
   - Prefer targeted fixes over broad refactors.

5. Apply fixes.
   - Keep behavior stable unless the Sonar issue requires a behavior change.
   - Typical C# fixes:
     - Extract helpers to reduce cognitive complexity (S3776)
     - Use `[LoggerMessage]` source generator for logging (S8969) — place in a
       dedicated `*.Log.cs` partial class file
     - Use async overloads with cancellation tokens (S6966)
     - Replace empty blocks with comments or remove dead code (S108)
     - Reorder method overloads to be adjacent (S4136)
     - Add `nameof()` to ArgumentException parameter names (S3928)
     - Use LINQ instead of manual loops where appropriate (S3267)

6. Verify locally.
   - Minimum:
     - `dotnet build --verbosity quiet` (must be 0 errors, 0 warnings due to TreatWarningsAsErrors)
     - `dotnet test src/Cisharpai.Tests/Cisharpai.Tests.csproj --verbosity quiet`
   - If a command fails, fix that before pushing.

7. Commit and push.
   - Use non-interactive git commands only.
   - Run:
     - `git add <files>`
     - `git commit -m "fix(sonar): <brief description>"`
     - `git push`

8. Recheck remote status.
   - Confirm branch state:
     - `git status --short --branch`
   - Check GitHub checks for the pushed commit:
     - `gh pr checks`
   - Requery SonarCloud:
     - `curl -fsSL "https://sonarcloud.io/api/project_pull_requests/list?project=alkampfergit_cisharpai"`
     - `curl -fsSL "https://sonarcloud.io/api/issues/search?componentKeys=alkampfergit_cisharpai&pullRequest=<prNumber>&ps=100"`

9. Report final state precisely.
   - Include:
     - PR number
     - files changed
     - local verification results
     - pushed commit SHA
     - whether SonarCloud has reanalyzed the latest commit yet
     - any remaining blocker outside the code changes

## Notes

- SonarCloud can lag behind GitHub pushes. If SonarCloud still shows an older `analysisDate` or commit SHA, say so explicitly instead of claiming the issues remain.
- This project has `TreatWarningsAsErrors=true` and `SonarAnalyzer.CSharp` as a build-time analyzer, so most code smells are caught locally before pushing.
- Do not close the loop with "ready to push". Push the branch unless the user explicitly tells you not to.
