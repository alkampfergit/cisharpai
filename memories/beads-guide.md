# Beads `bd show` Command - Complete Guide

> **Repository**: [github.com/steveyegge/beads](https://github.com/steveyegge/beads)

## Overview

The `bd show` command displays detailed information about Beads issues, including description, notes, history, dependencies, and metadata. It's your primary tool for viewing the full context of any issue.

---

## Basic Usage

### View an Issue

```bash
bd show <issue-id>
```

**Example:**
```bash
bd show bd-a1b2c3
```

**Sample Output:**
```
ID: bd-a1b2c3
Title: Fix login rate limiting
Type: bug
Priority: 0 (critical)
Status: open
Assignee: alice
Labels: backend, security, urgent
Created: 2026-01-28T20:15:00Z
Updated: 2026-01-28T22:30:00Z

Description:
Current login endpoint allows unlimited attempts. Implement rate
limiting with Redis at 5/min per IP. Also log suspicious activity.

Acceptance criteria:
- 429 response on excess attempts
- Reset window of 1 minute
- Logs to security audit table

Notes:
- 2026-01-28T21:45: Alice: "Discovered this while stress-testing"
- 2026-01-28T22:15: Bob: "Redis cluster ready, can start"

Blockers: none
Depends on: []
Blocks: bd-f14c (payment integration)
Discovered from: bd-5f8c (auth refactor)
```

### View Multiple Issues

```bash
bd show bd-a1b2 bd-f14c bd-3e7a
```

Shows side-by-side comparison of multiple issues.

---

## Finding Issue IDs

If you don't know the exact issue ID, use these commands:

```bash
# List all issues
bd list

# Search by title/keyword
bd list --search "login" | grep -o "bd-[a-f0-9]\{6\}"

# Get recent P0 issues
bd list --priority 0 --limit 5

# Get ready-to-work issues
bd ready --json | jq -r '.[].id'
```

**Extract and view in one command:**
```bash
ID=$(bd list --search "login" | head -1 | awk '{print $1}')
bd show $ID
```

---

## Output Formats

### Available Flags

| Flag | Purpose | Use Case |
|------|---------|----------|
| `--json` | Machine-readable JSON | Pipe to `jq`, automation scripts |
| `--markdown` | Clean Markdown export | Documentation, editing, sharing |
| `--history` | Full change history | Audit trail, understanding evolution |
| `--deps` | Dependency tree only | Understanding relationships |
| `--raw` | Unformatted database view | Debugging, raw data access |

### JSON Format

```bash
bd show bd-a1b2 --json
```

**Output:**
```json
{
  "id": "bd-a1b2c3",
  "title": "Fix login rate limiting",
  "description": "Current login endpoint allows unlimited attempts...\n\nAcceptance criteria:\n- 429 response...",
  "type": "bug",
  "priority": 0,
  "status": "open",
  "assignee": "alice",
  "labels": ["backend", "security", "urgent"],
  "created_at": "2026-01-28T20:15:00Z",
  "updated_at": "2026-01-28T22:30:00Z",
  "notes": [
    {"timestamp": "...", "author": "Alice", "text": "Discovered this while stress-testing"}
  ],
  "blockers": [],
  "depends_on": [],
  "blocks": ["bd-f14c"],
  "discovered_from": "bd-5f8c"
}
```

**JSON Processing Examples:**
```bash
# Extract just the description
bd show bd-a1b2 --json | jq -r .description

# Format notes as a list
bd show bd-a1b2 --json | jq -r '.notes[] | "- \(.timestamp | strftime("%H:%M")) \(.author): \(.text)"'

# Export all open issue descriptions
bd list --status open --json | jq -r '.[].description'
```

### Markdown Format

```bash
bd show bd-a1b2 --markdown
```

**Output:**
```markdown
# bd-a1b2c3: Fix login rate limiting

**Type**: bug | **Priority**: 0 | **Status**: open | **Assignee**: alice

## Description
Current login endpoint allows unlimited attempts...

## Notes
- **2026-01-28T21:45 Alice**: Discovered this while stress-testing
```

**Export to file:**
```bash
bd show bd-a1b2 --markdown > ticket.md
```

---

## Advanced Features

### Full History

View every change made to an issue:

```bash
bd show bd-a1b2 --history
```

**Output:**
```
bd-a1b2c3 change history:
2026-01-28T20:15:00Z Created (priority=0, type=bug)
2026-01-28T21:45:00Z Added label "security"
2026-01-28T22:15:00Z Assigned to alice
2026-01-28T22:30:00Z Note added: "Discovered during testing"
```

### Dependency Tree

Understand issue relationships:

```bash
bd show bd-a1b2 --deps
```

**Output:**
```
bd-a1b2c3 dependencies:
├── Blocks: bd-f14c (payment integration)
├── Discovered from: bd-5f8c (auth refactor)
└── Related: bd-9d3e (OAuth setup)
```

### Raw Database View

For debugging or advanced use:

```bash
bd show bd-a1b2 --raw
```

---

## Integration & Workflows

### Editor Integration

```bash
# Edit in Vim
bd show bd-a1b2 --markdown | vim -

# Save to file
bd show bd-a1b2 --markdown > detail.md

# Open in any editor
bd show bd-a1b2 --markdown | code -
```

### Claude Code Integration

Add to your **AGENTS.md**:

```markdown
## Viewing Issues

Before working on a task, always review it:

```bash
ID=$(bd ready --priority 0 --json | jq -r '.[0].id')
bd show $ID
```

This provides full context: description, acceptance criteria, notes, and blockers.
```

### Common Workflows

**1. Quick Review of Next Task**
```bash
bd ready --limit 1 | xargs bd show
```

**2. Deep Dive Before Starting Work**
```bash
ID=$(bd ready --priority 0 --json | jq -r '.[0].id')
echo "=== Working on: $ID ==="
bd show $ID --markdown
# ... read, think, plan ...
bd update $ID --status in_progress
```

**3. Audit Discovered Issues**
```bash
bd list --discovered-from bd-parent | xargs bd show --json
```

**4. Export All Open Issues**
```bash
for id in $(bd list --status open --json | jq -r '.[].id'); do
  bd show $id --markdown > "issues/$id.md"
done
```

---

## Visual Tools (Optional)

### bdui - Terminal UI

Interactive terminal interface for browsing issues.

```bash
# Install
npm i -g bdui

# Run
bdui

# Navigation
# - Arrow keys to navigate
# - Enter to view details
```

> **Repository**: [github.com/assimelha/bdui](https://github.com/assimelha/bdui)

### beads_viewer - Graph View

Interactive graph visualization with details pane.

```bash
# Install
go install github.com/Dicklesworthstone/beads_viewer@latest

# Run
bv

# Controls
# - Press 'i' for insights
# - Enter on node for details
```

---

## Quick Reference

| Task | Command |
|------|---------|
| View basic details | `bd show bd-a1b2` |
| Get JSON output | `bd show bd-a1b2 --json` |
| Export to Markdown | `bd show bd-a1b2 --markdown > file.md` |
| See full history | `bd show bd-a1b2 --history` |
| View dependencies | `bd show bd-a1b2 --deps` |
| View next ready task | `bd ready --limit 1 \| xargs bd show` |
| Compare multiple | `bd show bd-a1b2 bd-f14c` |
