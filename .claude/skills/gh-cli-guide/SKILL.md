---
name: gh-cli-guide
description: Reference guide for the `gh` GitHub CLI. Invoke when another skill (gstack-gh, gstack-full) needs canonical command patterns for listing, viewing, claiming, commenting, closing issues, creating PRs, watching checks, or managing labels. Also used when the user asks "how do I X with gh".
---

# gh CLI reference

This skill is a **reference** — it does not perform actions on its own. Other skills (especially `gstack-gh` and `gstack-full`) call into the command patterns below. When invoked directly by the user, print the relevant section and examples.

## Prerequisites

- `gh` installed (`gh --version`)
- Authenticated: `gh auth status` — if not, ask the user to run `! gh auth login` in the chat (interactive login).
- Repository context: either run inside a git repo with an `origin` remote, or pass `--repo OWNER/NAME` to every command.

## Authentication & context

```bash
gh auth status                      # check login + scopes
gh auth refresh -s repo,read:org    # grant extra scopes
gh repo view --json nameWithOwner   # confirm the current repo
```

## Issues

### List / search
```bash
gh issue list --state open --limit 50
gh issue list --label "automation" --state open --json number,title,labels,assignees
gh issue list --search "label:automation no:assignee" --json number,title
gh search issues "repo:OWNER/NAME label:ready state:open" --json number,title,url
```

### View
```bash
gh issue view 123 --json number,title,body,labels,assignees,state,comments
gh issue view 123 --comments         # human-readable with comments
```

### Claim (assign + comment + label)
```bash
gh issue edit 123 --add-assignee @me --add-label "in-progress"
gh issue comment 123 --body "Picked up by Claude. Starting implementation."
```

### Close
```bash
gh issue close 123 --comment "Resolved by #456"
gh issue edit 123 --remove-label "in-progress" --add-label "done"
```

## Pull requests

### Create
```bash
gh pr create --title "feat: X" --body-file PR_BODY.md --base develop
gh pr create --fill                  # use commit message
gh pr create --draft --title "..."   # open as draft
```

### View / list
```bash
gh pr view                           # current branch's PR
gh pr view 456 --json state,mergeable,statusCheckRollup
gh pr list --state open --author @me
```

### Checks
```bash
gh pr checks                         # quick status of current PR
gh pr checks 456 --watch             # block until all checks complete
gh run list --branch <branch> --limit 5
gh run view <run-id> --log-failed    # fetch failing logs
```

### Merge
```bash
gh pr merge 456 --squash --delete-branch
gh pr merge 456 --auto --squash      # enable auto-merge when checks pass
```

## Labels

```bash
gh label list
gh label create "automation" --color FFD700 --description "Auto-implementable by Claude"
gh issue edit 123 --add-label "automation"
```

## Direct API (for things not covered above)

```bash
gh api repos/OWNER/NAME/issues?labels=automation&state=open
gh api graphql -f query='...'        # for complex queries
gh api repos/OWNER/NAME/issues/123/timeline --paginate
```

## Useful JSON + jq patterns

`gh` emits JSON with `--json <fields>` and can format with `--jq '...'`:

```bash
gh issue list --label automation --state open \
  --json number,title,labels \
  --jq '.[] | select([.labels[].name] | index("in-progress") | not) | {number, title}'
```

This pattern finds issues with a given label that are **not yet** marked in-progress — useful for pollers that want idempotency.

## Conventions used by gstack-gh / gstack-full

The automation skills in this repo rely on these markers:

| Purpose            | Mechanism                                  |
| ------------------ | ------------------------------------------ |
| Queue flag         | A label chosen by the user (e.g. `ready-for-claude`) |
| Claimed            | Label `in-progress` + assignee `@me`       |
| Completed          | PR merged, issue closed with PR reference  |
| Failed / skipped   | Label `needs-human` + comment with reason  |

Keep these label names configurable via the invoking skill's args.
