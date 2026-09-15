# Implementation Plan: Web Search as Retrieval

**Issue**: #73
**Status**: Complete

## Phase 1: Core Abstractions

1. `IWebSearchFeature` interface in `src/Cisharpai/Features/Chat/`
2. `WebSearchOptions` record in `src/Cisharpai/Models/`
3. `ChatCompletionResponse.WebSearchCount` init property
4. `GroundingKind.WebSearch` enum value

## Phase 2: Anthropic Provider

1. `AnthropicClientOptions.WebSearchToolVersion` configuration
2. `AnthropicToolDefinition.Type` property for web search tool version
3. `AnthropicServerToolUse` / `AnthropicUsage.ServerToolUse` for billing data
4. `AnthropicCitationResult.Url` for `web_search_result_location` citations
5. `InjectWebSearchTool` / `BuildWebSearchResponse` / `ExtractWebSearchContentAndCitations` methods
6. Feature registration on `AnthropicChatCompletionClient`

## Phase 3: OpenAI Provider

1. `OpenAiResponsesApiRequest.Tools` / `OpenAiResponsesApiTool` for tool injection
2. `OpenAiAnnotation.Url` / `Title` for `url_citation` annotations
3. `MapWebSearchResponse` / `MapWebSearchAnnotationsToCitations` methods
4. GPT-5-only guard (returns `IsSuccess=false` for non-GPT-5 models)
5. Feature registration on `OpenAiChatCompletionClient`

## Phase 4: Testing

1. `FakeChatCompletionClient` — queue, default, capture, feature flag for `IWebSearchFeature`
2. `FakeResponses.WebSearch()` factory
3. `FakeChatFeatures.WebSearch` flag
4. Unit tests: Anthropic (request building, response mapping, error handling, feature discovery)
5. Unit tests: OpenAI (request building, response mapping, error handling, feature discovery)
6. Unit tests: fakes (queue, default, capture, reset, feature flag)

## Phase 5: Documentation

1. `RELEASE_NOTES.md`
2. `wiki/provider-features.md`
3. `wiki/testing.md`
4. `wiki/web-search.md` (new)
5. `wiki/index.md`
6. `memories/project_overview.md`
7. `llm/cisharpai-expert/SKILL.md` + `references/web-search.md`
8. `specs/073-web-search-retrieval/`
