# Beads `bd show` Command - Complete Guide

> **Repository**: [github.com/steveyegge/beads](https://github.com/steveyegge/beads)

## Output Formats

| Flag | Purpose | Use Case |
|------|---------|----------|
| `--json` | Machine-readable JSON | Pipe to `jq`, automation scripts |
| `--markdown` | Clean Markdown export | Documentation, editing, sharing |
| `--history` | Full change history | Audit trail, understanding evolution |
| `--deps` | Dependency tree only | Understanding relationships |
| `--raw` | Unformatted database view | Debugging, raw data access |

## JSON Processing Examples

```bash
# Extract just the description
bd show bd-a1b2 --json | jq -r .description

# Format notes as a list
bd show bd-a1b2 --json | jq -r '.notes[] | "- \(.timestamp | strftime("%H:%M")) \(.author): \(.text)"'

# Export all open issue descriptions
bd list --status open --json | jq -r '.[].description'
```

## Finding Issue IDs

```bash
# Search by title/keyword
bd list --search "login" | grep -o "bd-[a-f0-9]\{6\}"

# Get recent P0 issues
bd list --priority 0 --limit 5

# Get ready-to-work issues
bd ready --json | jq -r '.[].id'
```

## Common Workflows

**Quick Review of Next Task:**
```bash
bd ready --limit 1 | xargs bd show
```

**Deep Dive Before Starting Work:**
```bash
ID=$(bd ready --priority 0 --json | jq -r '.[0].id')
bd show $ID --markdown
bd update $ID --status in_progress
```

**Export All Open Issues:**
```bash
for id in $(bd list --status open --json | jq -r '.[].id'); do
  bd show $id --markdown > "issues/$id.md"
done
```

## Visual Tools (Optional)

- **bdui** — Interactive terminal UI: `npm i -g bdui` then `bdui` ([github.com/assimelha/bdui](https://github.com/assimelha/bdui))
- **beads_viewer** — Graph visualization: `go install github.com/Dicklesworthstone/beads_viewer@latest` then `bv`
