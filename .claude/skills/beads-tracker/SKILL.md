---
name: beads-tracker
description: >
  Manage project issues using the bd (beads) CLI tool. Use when the user
  mentions "beads", "bd", "issues", "stories", "backlog", asks to "show work",
  "find available tasks", "close an issue", "update status", or references
  a beads issue ID (e.g. bd-xxxx or cisharpai-xxx).
---

# Beads Issue Tracker

## Rules

- NEVER read `.beads/issues.jsonl` directly — always use the `bd` CLI.

## Quick Reference

```bash
bd ready                                  # Find available work
bd show <id>                              # View issue details
bd show <id> --json                       # JSON output for scripting
bd show <id> --deps                       # View dependency tree
bd update <id> --status in_progress       # Claim work
bd close <id>                             # Complete work
bd sync                                   # Sync with git
bd dep add <blocked-id> <blocker-id>      # Add dependency
bd list                                   # List all issues
bd list --search "keyword"                # Search issues
```

## Typical Workflow

1. **Find work**: `bd ready` to see available tasks
2. **Review**: `bd show <id>` for full context (description, acceptance criteria, notes, blockers)
3. **Claim**: `bd update <id> --status in_progress`
4. **Implement**: Make changes, write tests, verify green
5. **Close**: `bd close <id>`
6. **Sync**: `bd sync` to sync state with git

## Detailed Reference

For full `bd show` options, output formats, and advanced workflows see
[references/bd-show-guide.md](references/bd-show-guide.md).
