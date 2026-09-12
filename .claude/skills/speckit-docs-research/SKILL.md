---
name: "speckit-docs-research"
description: "Search Microsoft Learn and Context7 MCP servers for documentation relevant to the current feature before specification or planning."
argument-hint: "Feature description or topics to research"
compatibility: "Requires .mcp.json with microsoft-learn and context7 servers configured"
metadata:
  author: "cisharpai"
user-invocable: true
disable-model-invocation: false
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Goal

Search external documentation sources (Microsoft Learn, Context7) for technical context relevant to the current feature. Write findings with source URLs to the spec directory so the specification and plan can cite them.

## Step 1 — Identify Topics

Determine what to search for:

1. If user input is provided, extract technical topics from it.
2. Otherwise, look for the current spec directory (`specs/NNN-*/spec.md`) and extract topics from the feature description.
3. Focus on: .NET APIs/namespaces, LLM provider APIs, protocols (MCP, HTTP, SSE), Azure services, NuGet packages, and any library/framework names.

## Step 2 — Query Microsoft Learn

**Fail soft**: if the server is unreachable, log `⚠ microsoft-learn MCP unavailable, skipping` and continue.

- Use `microsoft_docs_search` with each identified .NET/Azure/Microsoft topic.
- Use `microsoft_code_sample_search` for topics where code examples would be valuable.
- Collect the top 2-3 results per topic — titles, URLs, and key excerpts.

## Step 3 — Query Context7

**Fail soft**: if the server is unreachable, log `⚠ context7 MCP unavailable, skipping` and continue.

- Use `resolve-library-id` to find library identifiers for relevant SDKs and packages.
- Use `query-docs` with resolved library IDs to retrieve API reference, usage patterns, and configuration docs.
- Collect the top 2-3 results per library — titles, URLs, and key excerpts.

## Step 4 — Write Findings

If a spec directory exists (`specs/NNN-*/`), write to `specs/NNN-*/research-docs.md`.
If no spec directory exists yet (running before specify creates it), hold findings in context for the calling phase to pick up and include later.

Format:

```markdown
# Documentation Research

## Topics Researched
- {topic 1}
- {topic 2}

## Findings

### {Topic}

**Source**: {URL}
**Relevance**: {one-line summary}

- {Key point 1}
- {Key point 2}

---

## Summary

{2-3 sentence synthesis for the feature implementation}
```

## Error Handling

- If **both** servers are unreachable: output `⚠ Documentation research skipped — no MCP servers available` and return successfully. Never block the calling phase.
- If one server fails: research with the other and note which was unavailable.
- Never throw an error — this command is informational only.
