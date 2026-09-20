---
description: "Search Microsoft Learn and Context7 for relevant documentation before specification or planning"
---

# Documentation Research

Query external documentation sources to gather relevant technical context for the current feature. Findings are written to the spec directory so the specification and plan can cite them.

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Context Discovery

1. Determine the current feature by reading the spec directory (look for `specs/NNN-*/spec.md` or the feature description passed as input).
2. Extract the key technical topics from the feature description — focus on:
   - .NET APIs, namespaces, or packages mentioned
   - LLM provider APIs (OpenAI, Anthropic, Cohere, Azure)
   - Protocols or standards (MCP, HTTP, SSE, JSON Schema)
   - Azure services or SDK surface areas
   - Any library or framework names

## Research Phase

For each identified topic, query the available MCP servers. **Fail soft**: if a server is unreachable or returns an error, log one line (`⚠ {server} unavailable, skipping`) and continue with the remaining sources. A documentation lookup must never block the specify or plan phase.

### Microsoft Learn

Use the `microsoft_docs_search` tool to search for:
- .NET API documentation relevant to the feature
- Azure service documentation if Azure providers are involved
- Best practices and architectural guidance

Use `microsoft_code_sample_search` to find:
- Official code samples demonstrating the relevant APIs

### Context7

Use `resolve-library-id` to find library identifiers for:
- Provider SDKs (e.g., OpenAI .NET SDK, Azure.AI.Inference)
- Relevant NuGet packages mentioned in the feature

Then use `query-docs` with the resolved library IDs to retrieve:
- API reference and usage patterns
- Migration guides or breaking changes
- Configuration and setup documentation

## Output

Write findings to `research-docs.md` in the current spec directory (`specs/NNN-*/research-docs.md`). If the spec directory does not exist yet (running before specify creates it), write to a temporary location that the specify phase will pick up, or hold the findings in context for the current phase.

Format the output as:

```markdown
# Documentation Research

## Topics Researched
- {topic 1}
- {topic 2}

## Findings

### {Topic}

**Source**: {URL}
**Relevance**: {one-line summary of why this matters for the feature}

{Key excerpts or summaries — keep concise, 2-5 bullet points per source}

---

## Summary

{2-3 sentence synthesis of what the documentation tells us about implementing this feature}
```

## Error Handling

- If **both** MCP servers are unreachable, output: `⚠ Documentation research skipped — no MCP servers available` and return successfully.
- If one server fails, research with the other and note which was unavailable.
- Never throw an error or block the calling phase — this hook is informational only.
