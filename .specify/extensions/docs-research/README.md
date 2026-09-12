# docs-research extension

Queries Microsoft Learn and Context7 MCP servers for relevant documentation before the specification and planning phases. Findings are written to `specs/NNN-*/research-docs.md` with source URLs so the spec and plan can cite authoritative references.

## Hooks

| Phase | Command | Optional |
|-------|---------|----------|
| `before_specify` | `speckit.docs.research` | No |
| `before_plan` | `speckit.docs.research` | No |

## MCP Servers Used

- **Microsoft Learn** (`microsoft_docs_search`, `microsoft_code_sample_search`) — .NET, Azure, and Microsoft platform docs
- **Context7** (`resolve-library-id`, `query-docs`) — third-party library and SDK documentation

Both servers must be configured in `.mcp.json` (Claude Code) and/or `codex.toml` (Codex). The hook fails soft if a server is unreachable.
