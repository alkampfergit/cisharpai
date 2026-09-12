# Spec Kit Maintenance Guide

This project uses [GitHub Spec Kit](https://github.com/github/spec-kit) for spec-driven development. The `specify` CLI manages the integration, which installs and maintains Claude Code skills (`.claude/skills/speckit-*`) and shared infrastructure (`.specify/`).

## Checking the current version

```bash
specify --version
```

## Upgrading the CLI

```bash
specify self check          # see if a newer release exists
specify self upgrade        # upgrade in place
```

The CLI is installed via `uv tool` (or `pipx`). If `self upgrade` fails, the manual fallback is shown by `self check`.

## Upgrading skills and integration files

After upgrading the CLI, the local skills and templates may be out of date. Run:

```bash
specify integration status          # verify current state
specify integration upgrade claude  # upgrade managed skills
```

If the upgrade reports that shared infrastructure paths were not refreshed, add `--force`:

```bash
specify integration upgrade claude --force
```

This updates all files tracked in `.specify/integrations/claude.manifest.json` and `.specify/integrations/speckit.manifest.json`, including:

- `.claude/skills/speckit-*/SKILL.md` (managed skills)
- `.specify/templates/` (spec, plan, tasks, checklist templates)
- `.specify/scripts/bash/` (helper scripts)

### What gets updated vs. what stays

| Category | Updated by `upgrade` | Notes |
|----------|---------------------|-------|
| Managed skills (speckit-analyze, speckit-clarify, speckit-plan, etc.) | Yes | Listed in `claude.manifest.json` |
| Git skills (speckit-git-commit, speckit-git-feature, etc.) | Yes | Listed in `speckit.manifest.json` |
| Custom/project skills (speckit-full, speckit-gh, speckit-retrospec, gitflow) | No | Not in any manifest; maintained manually |
| Shared templates and scripts (`.specify/`) | Yes (with `--force`) | |
| `.claude/settings.json` | May be updated | Review changes before committing |

## Marketplace plugin skills

Some skills come from the [agent-plugins-base](https://github.com/alkampfergit/agent-plugins-base) marketplace plugin (`github-alk`) rather than Spec Kit:

- `gh-actions-debug`
- `gh-cli-guide`
- `github-pr-manager`
- `sonarcloud`
- `sonarcloud-pr-fix`

These are installed per-user (not checked into the repo) via:

```bash
claude plugin marketplace add https://github.com/alkampfergit/agent-plugins-base.git
claude plugin install github-alk@agent-plugins-base
```

Update with:

```bash
claude plugin marketplace update agent-plugins-base
```

Skills from this plugin are referenced with the `github-alk:` prefix in skill files (e.g., `github-alk:gh-cli-guide`). Do not create local copies of these skills in `.claude/skills/`.

## Full update checklist

1. `specify self upgrade` — upgrade the CLI
2. `specify integration upgrade claude --force` — refresh managed skills and templates
3. `git diff` — review what changed
4. Test that skills still load: invoke one (e.g., `/speckit-plan`) and verify no errors
5. `claude plugin marketplace update agent-plugins-base` — update marketplace plugin
6. Commit the changes

## Useful commands

| Command | Purpose |
|---------|---------|
| `specify --version` | Current CLI version |
| `specify self check` | Check for newer CLI release |
| `specify integration status` | Show installed integrations and health |
| `specify integration list` | List available integrations |
| `specify integration info claude` | Details on the claude integration |
| `claude plugin list` | List installed marketplace plugins |
