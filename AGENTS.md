# Agent Instructions

This project uses **bd** (beads) for issue tracking. Run `bd onboard` to get started.

## RULES

- NEVER read the .beads/issues.jsonl directly always use bd commandline (see below)

## Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --status in_progress  # Claim work
bd close <id>         # Complete work
bd sync               # Sync with git
bd dep                # manage dependencies es: bd dep add <blocked-id> <blocker-id>
```


