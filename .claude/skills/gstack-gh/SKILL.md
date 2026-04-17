---
name: gstack-gh
description: Take a single GitHub issue (by number or URL) and drive it through gstack's end-to-end flow (plan → implement → QA → ship) using the `gh` CLI. Use when the user says "implement issue #N", "work this GH issue", "take issue X through gstack", or passes a GitHub issue link. For label-based polling across many issues, use `gstack-full` instead.
---

# gstack-gh — one issue, end-to-end

Drive a single GitHub issue through gstack's sprint flow automatically. The user provides an issue identifier; this skill handles claim → plan → build → test → ship.

**Reference:** for `gh` command patterns, read the sibling skill `gh-cli-guide/SKILL.md`.

## Inputs (from args)

Accept any of:

- Plain number: `123` (uses current repo)
- Owner/repo plus number: `owner/repo#123`
- Full URL: `https://github.com/owner/repo/issues/123`

Optional args as `key=value`:
- `claim-label` (default `in-progress`)
- `done-label` (default `done`)
- `fail-label` (default `needs-human`)
- `base` branch (default: repo default branch)
- `dry-run=true` — do plan + diff only, no push or PR

Parse these up-front; confirm resolved values back to the user in one line before proceeding.

## Preconditions (fail fast with a clear message)

1. `gh auth status` — abort if not authenticated; tell the user to run `! gh auth login`.
2. Working tree is clean (`git status --porcelain` empty). If dirty, stop and ask.
3. Current branch is the project's base (e.g. `develop`). If not, offer to switch.
4. Issue is open, unassigned (or assigned to `@me`), and does NOT already carry `claim-label`. If it does, assume another run is in flight and abort with guidance.

## Flow

### 1. Fetch & understand
```bash
gh issue view <N> --json number,title,body,labels,assignees,state,comments --repo <owner/repo>
```
Summarise the issue in ≤3 bullets to the user. Identify:
- Acceptance criteria
- Files/areas likely affected (quick `tokensave_context` query — this repo has tokensave initialised)
- Any linked PRs or dependent issues

### 2. Claim
```bash
gh issue edit <N> --add-assignee @me --add-label <claim-label>
gh issue comment <N> --body "Picked up by Claude via gstack-gh. Branch: <branch-name>"
```
Create a working branch: `feature/issue-<N>-<short-slug>`.

### 3. Plan (gstack)
Invoke `/plan-eng-review` in **non-interactive** mode where possible. If the issue is small/obvious, skip to implementation with a 3-line plan printed to chat. For anything touching architecture, public API, new provider, or >5 files: use `/plan-eng-review` and stop for user approval before continuing (unless `dry-run=true` — in that case finish after planning).

Project-specific: this repo has mandatory rules in `AGENTS.md` and `.claude/CLAUDE.md` (e.g. multi-target .NET 8/10, NSubstitute for mocks, tests must be green, update `memories/project_overview.md` when structure changes, **never read `.env`**). Surface any of these that apply before building.

### 4. Build
Implement the change. Follow the Post-Change Checklist in `.claude/CLAUDE.md` when applicable (wiki updates, `project_overview.md`, `Cisharpai.Testing` additions, `scripts/build.ps1` pack list).

### 5. Test
Run unit tests locally for both target frameworks:
```bash
dotnet test src/Cisharpai.Tests/Cisharpai.Tests.csproj -f net8.0
dotnet test src/Cisharpai.Tests/Cisharpai.Tests.csproj -f net10.0
```
Do NOT run integration tests (they hit real APIs) unless the user explicitly asks. If tests fail, fix — don't mark the issue done with red tests (see CLAUDE.md: "Do not consider task finished if tests are not green").

Optionally run `/qa` if the change is UI/web-facing. This repo is a library, so `/qa` is usually skipped.

### 6. Ship
Use `/ship` when available; otherwise fall back to:
```bash
git push -u origin <branch>
gh pr create --base <base> --title "fix(#<N>): <title>" \
  --body-file <(cat <<'EOF'
## Summary
- <what/why, 1-3 bullets>

Closes #<N>

## Test plan
- [ ] unit tests net8.0 green
- [ ] unit tests net10.0 green
EOF
)
```
The `Closes #<N>` line auto-closes the issue on merge.

### 7. Hand off
Post a summary comment on the issue with the PR URL:
```bash
gh issue comment <N> --body "Implementation ready for review: <pr-url>"
```
Print the PR URL to the user. **Do not** merge or land — that is the human's call (or a separate `/land-and-deploy` invocation).

## Failure handling

If any step fails and cannot be recovered automatically:
1. Remove `claim-label`, add `fail-label`.
2. Post a comment on the issue with what failed, what was tried, and what's needed from a human.
3. Leave the working branch intact so the user can inspect.
4. Surface the failure clearly in chat — do not pretend it succeeded.

## What this skill does NOT do

- Does not merge PRs (use `/land-and-deploy`).
- Does not re-plan interactively without user involvement for significant scope.
- Does not touch `.env` or read secrets.
- Does not run integration tests (cost + external dependencies).
