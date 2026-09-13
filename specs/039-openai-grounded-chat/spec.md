# Feature Specification: OpenAI / Azure OpenAI Grounded Chat with Citations

**Issue**: #39
**Status**: Complete
**Created**: 2026-09-12

## Summary

Register `IGroundedChatFeature` on `OpenAiChatCompletionClient` and `AzureOpenAiChatCompletionClient` using the Responses API's `input_file` transport for inline document grounding with citations.

## Design Decisions

1. **Transport**: `input_file` items with base64-encoded content (owner-confirmed preference over `context` parameter).
2. **Non-GPT-5 models**: Return `IsSuccess=false` with descriptive error. No silent fallback to prompt-injection (#40).
3. **CitationMode**: Accepted but ignored for OpenAI/Azure — annotations are always returned.
4. **Azure routing**: Reuses `ExecuteWithRouteFallbackAsync`. If deployment falls back to Chat Completions, returns error.

## Acceptance Criteria

- [x] `IGroundedChatFeature` registered on both clients
- [x] Documents supplied through `GroundedChatOptions.Documents` reach model as `input_file` items
- [x] Annotations mapped to `Citation`/`CitationSource` with `DocumentChunk.Id` preserved
- [x] Non-Responses-API models return `IsSuccess=false`
- [x] Azure variant reuses routing machinery and respects `ModelName`
- [x] Non-throwing failures, `RawRequestJson`/`RawResponseJson`
- [x] Unit tests (OpenAI + Azure)
- [x] Feature discovery tests updated
- [x] Wiki updated (grounded-chat.md, provider-features.md, openai.md)
- [x] RELEASE_NOTES.md, project_overview.md, cisharpai-expert skill updated
