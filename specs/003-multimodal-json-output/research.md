# Research: Multimodal Images & JSON Output

Technical decisions observed in the implementation, with inferred rationale and rejected alternatives.

## Decision 1: Discriminated Union via Abstract Record for Content Parts

**Decision**: `MessageContentPart` is an abstract record with three sealed subtypes (`TextContentPart`, `ImageFileContentPart`, `ImageBase64ContentPart`). Pattern matching in providers handles each type.

**Rationale**: C# sealed record subtypes create a closed set that is exhaustively matchable in `switch` expressions, which providers use to map content parts to their wire format. The abstract base type enables polymorphic collections (`IReadOnlyList<MessageContentPart>`) without losing type safety.

**Alternatives rejected**:
- **Single class with nullable fields**: Loses type safety; callers could create invalid combinations (e.g., both `FilePath` and `Base64Data` set).
- **Enum + data class**: Less ergonomic than pattern matching on record types.
- **Interface hierarchy**: Records provide value equality for free; interfaces would require manual equality.

## Decision 2: ContentParts Property on LlmMessage (Not a Separate Type)

**Decision**: Multimodal content is an optional `ContentParts` property on the existing `LlmMessage` record, not a separate message type.

**Rationale**: This preserves backward compatibility — text-only callers continue using `Content` as before, and the check `if (ContentParts is not null)` in each provider naturally falls through to the text path. A separate message type would require all message-handling code to handle two types or use a common interface.

**Alternatives rejected**:
- **Separate `MultimodalMessage` type**: Would require union types or `OneOf<LlmMessage, MultimodalMessage>` in message lists, complicating every provider.
- **Replace `Content` with `ContentParts` always**: Breaking change for all existing callers.
- **Wrapper class**: Adds unnecessary indirection.

## Decision 3: Generic Mapper in ContentPartHelper

**Decision**: `ContentPartHelper.MapOpenAiStyleContentPartsAsync<T>` takes lambda factories (`createTextPart`, `createImageUrlPart`) and returns `List<T>`, reused by OpenAI, Azure OpenAI, and Azure AI Inference.

**Rationale**: All three OpenAI-compatible providers use identical image format (`image_url` with data URI) but different wire DTO types (`OpenAiContentPart`, `AzureOpenAiContentPart`, etc.). The generic mapper avoids duplicating the file-reading and base64-encoding logic while letting each provider supply its own DTO constructor.

**Alternatives rejected**:
- **Shared base wire DTO**: Would couple provider packages to a common wire type.
- **Per-provider copy-paste**: Three copies of the same file I/O and encoding logic.
- **Return raw strings and let providers wrap**: Loses the type safety of the generic approach.

## Decision 4: System Message Injection for JSON Mode

**Decision**: `JsonOutputHelper.EnsureJsonKeywordInSystemMessage` automatically appends "Respond in JSON." to the system message if the word "JSON" is not already present, but only for `JsonMode`.

**Rationale**: OpenAI-compatible models require the system message to mention "JSON" when using `response_format: {type: "json_object"}`. Without this, the API returns an error. Auto-injection provides a better DX than forcing callers to remember this requirement. The check is case-insensitive and idempotent.

**Alternatives rejected**:
- **Require callers to include "JSON"**: Poor DX; easy to forget, results in cryptic API errors.
- **Always inject regardless of mode**: Would modify system messages for JsonSchema mode where it's not needed.
- **Inject for all providers**: Anthropic and Cohere don't need it, but the injection is harmless. The helper is called by all providers for simplicity.

## Decision 5: Code Fence Stripping for Anthropic JSON Mode

**Decision**: `JsonOutputHelper.StripMarkdownCodeFences` removes ` ```json ... ``` ` wrapping. Used by Anthropic (which doesn't have a native `json_object` response format).

**Rationale**: Anthropic's JSON mode is implemented via system message injection ("Respond in JSON.") rather than a native API parameter. Some Anthropic models wrap their JSON output in markdown code fences. Stripping these ensures callers always get clean JSON regardless of model behavior.

**Alternatives rejected**:
- **Let callers handle it**: Inconsistent experience; some providers return clean JSON, others don't.
- **Use regex**: The fence format is simple enough for string operations; regex would be overkill.

## Decision 6: Anthropic Base64 Source Blocks (Not Data URIs)

**Decision**: Anthropic receives images as `{type: "base64", media_type: "...", data: "..."}` source blocks, not data URIs.

**Rationale**: This is dictated by Anthropic's API — they do not accept `data:` URIs in image content. The library extracts the raw base64 data and MIME type from `ImageBase64ContentPart` or reads them via `ImageDataUriHelper`, then formats them as Anthropic expects. The abstraction means callers don't need to know about this difference.

**Alternatives rejected**:
- **Send data URIs to Anthropic**: Would cause API errors.
- **Require callers to format Anthropic-style**: Leaks provider details through the abstraction.

## Decision 7: Cohere Silent Image Skipping

**Decision**: When Cohere receives a message with `ContentParts`, image parts are silently skipped and only text parts are extracted.

**Rationale**: Cohere's chat API does not support image input. Throwing an exception would prevent cross-provider code from working. Silent skipping with text extraction is the safest degradation — the text content still reaches the model. This is documented in the wiki.

**Alternatives rejected**:
- **Throw NotSupportedException**: Would force provider-specific branching in cross-provider code.
- **Return an error response**: Would treat a known limitation as an API failure.
- **Skip the entire message**: Would lose the text content, which may still be valuable.

## Decision 8: Separate Wire DTOs Per Provider for Response Format

**Decision**: Each provider has its own response format wire DTO (`OpenAiResponseFormat`, `AzureOpenAiResponseFormat`, `AzureAiInferenceResponseFormat`, `AnthropicOutputConfig`, `CohereChatResponseFormat`) even though several are structurally identical.

**Rationale**: Provider packages are independent NuGet packages that should not reference each other. Sharing wire DTOs would require a common package or cross-references. The duplication is minimal (small sealed classes) and keeps each provider self-contained.

**Alternatives rejected**:
- **Shared wire DTOs in core**: Would couple the core abstractions package to provider-specific serialization details.
- **Share between Azure and OpenAI**: Azure and OpenAI are in separate packages.

## Decision 9: OpenAI Responses API Text.Format for GPT-5

**Decision**: The OpenAI provider uses a different wire format (`text.format`) for JSON output with GPT-5 (Responses API) vs. legacy models (`response_format` in Chat Completions API). The `OpenAiTextFormat` DTO flattens the schema fields into the format object instead of nesting them.

**Rationale**: OpenAI's Responses API uses a different JSON structure than the Chat Completions API. The library detects the model type and routes to the correct API and wire format, keeping this transparent to callers.

**Alternatives rejected**:
- **Force all models through one API**: GPT-5 requires the Responses API; can't use Chat Completions.
- **Expose two methods**: Would leak API routing details to callers.
