# Feature Specification: Web Search as Retrieval via Provider Server-Side Tools

**Issue**: #73
**Status**: Complete
**Created**: 2026-09-15

## Summary

New `IWebSearchFeature` enables provider-hosted server-side web search. The model decides whether to search, executes the search within a single request (no client-side round trip), and returns cited answers via the existing `GroundedChatCompletionResponse` type.

## Design Decisions

1. **Return type**: `GroundedChatCompletionResponse` (reuse existing grounded chat response type).
2. **Citation mapping**: `CitationSource.Id` = page URL, `CitationSource.Data["title"]` = page title (consistent with `search_result_location` pattern from #76). No new `Url` property on `CitationSource` — declined by owner.
3. **WebSearchCount**: `int? init` property on `ChatCompletionResponse` (NOT a positional parameter, to avoid binary-breaking changes like #76).
4. **GroundingKind.WebSearch**: New enum value to distinguish web-search from document-grounded citations. Owner verified no exhaustive switches exist.
5. **Anthropic tool version**: Configurable via `AnthropicClientOptions.WebSearchToolVersion` (default `web_search_20260209`).
6. **OpenAI GPT-5 only**: Non-GPT-5 models return `IsSuccess=false` with descriptive error.
7. **No raw search results**: `RawResponseJson` suffices for debugging.

## Acceptance Criteria

- [x] `IWebSearchFeature` registered on Anthropic and OpenAI clients
- [x] Anthropic: `web_search` tool injected with configured version, `web_search_result_location` citations mapped
- [x] OpenAI: Responses API `web_search` tool, `url_citation` annotations mapped
- [x] `WebSearchCount` populated from provider-specific billing data
- [x] `GroundingKind.WebSearch` set on response
- [x] Non-GPT-5 OpenAI models return `IsSuccess=false`
- [x] `FakeChatCompletionClient` with queue/default/capture for `IWebSearchFeature`
- [x] `FakeResponses.WebSearch()` factory method
- [x] `FakeChatFeatures.WebSearch` flag
- [x] Unit tests (Anthropic, OpenAI, fakes) — 55 tests total
- [x] Wiki: `web-search.md`, `provider-features.md`, `testing.md`, `index.md`
- [x] RELEASE_NOTES.md, project_overview.md, SKILL.md updated
