# Requirements Checklist: ExtraParameters & Deep Merge

## Functional Requirements

- [x] **FR-001**: Optional `ExtraParameters` JSON object on chat completion requests
  - Implemented: `ChatCompletionRequest.ExtraParameters` (JsonElement?); tested across all 5 providers
- [x] **FR-002**: Optional `ExtraParameters` JSON object on embedding requests
  - Implemented: `EmbeddingRequest.ExtraParameters` (JsonElement?)
- [x] **FR-003**: Deep-merge `ExtraParameters` into serialized request body before sending
  - Implemented: `LlmHttpClient.SerializeAndMerge()`; test: `PostAsync_MergesExtraParameters_IntoRequestBody`
- [x] **FR-004**: Recursive merge of nested JSON objects
  - Verified: `Merge_DeepMergesNestedObjects`, `Merge_DeeplyNestedMerge_ThreeLevels`, `Merge_NestedObjectWithNewProperty_InBase`
- [x] **FR-005**: Arrays, scalars, booleans, and nulls are replaced (not merged)
  - Verified: `Merge_OverrideReplacesArray`, `Merge_OverrideIntegerProperty`, `Merge_OverrideBooleanProperty`, `Merge_OverrideCanSetNullValue`
- [x] **FR-006**: Overrides can replace a property value regardless of type
  - Verified: `Merge_OverrideReplacesObjectWithScalar`, `Merge_OverrideReplacesScalarWithObject`, `Merge_OverrideStringWithNumber`
- [x] **FR-007**: Base properties not in override are preserved
  - Verified: `Merge_AddsNewTopLevelProperty` (model preserved), `Merge_DeepMergesNestedObjects` (effort preserved)
- [x] **FR-008**: Override properties not in base are added
  - Verified: `Merge_AddsNewTopLevelProperty`, `Merge_MultipleNewProperties`, `Merge_EmptyBase_ReturnsOverrides`
- [x] **FR-009**: ArgumentException for non-object base or override
  - Verified: `Merge_ThrowsWhenOverridesNotObject`, `Merge_ThrowsWhenBaseNotObject`
- [x] **FR-010**: Skip merge when ExtraParameters is null
  - Verified: `PostAsync_ExtraParametersNull_SendsOriginalPayloadOnly`, `ExtraParameters_Null_NoMergeOccurs`
- [x] **FR-011**: `IncludeRawResponse` flag on both chat and embedding requests
  - Implemented: `ChatCompletionRequest.IncludeRawResponse`, `EmbeddingRequest.IncludeRawResponse`
- [x] **FR-012**: Raw JSON capture when IncludeRawResponse is true
  - Verified: `IncludeRawResponse_ReturnsRawRequestJson_WithMergedPayload` (OpenAI), `IncludeRawResponse_ReturnsRawRequestJson` (Anthropic, Azure OpenAI), `IncludeRawResponse_ReturnsBothRawJsons` (Azure AI Inference)
- [x] **FR-013**: RawRequestJson and RawResponseJson are null when IncludeRawResponse is false
  - Verified: `IncludeRawResponse_WithoutExtraParameters_StillReturnsRawRequestJson` confirms opt-in behavior
- [x] **FR-014**: ExtraParameters works with all 5 chat providers
  - Verified: `OpenAiExtraParametersTests` (7), `AzureOpenAiExtraParametersTests` (8), `AzureAiInferenceChatCompletionClientTests` (2), `AnthropicExtraParametersTests` (3); Cohere uses `LlmHttpClient.PostAsync` with `request.ExtraParameters`
- [x] **FR-015**: ExtraParameters applies to streaming, tool calling, JSON output, and grounded chat paths
  - Implemented: All feature methods pass `request.ExtraParameters` through to `LlmHttpClient`; `PostStreamAsync` also calls `SerializeAndMerge`

## User Stories

- [x] **US-1**: Inject arbitrary parameters into requests (P1)
- [x] **US-2**: Deep merge nested JSON objects (P1)
- [x] **US-3**: Inspect raw wire-level payloads (P1)
- [x] **US-4**: ExtraParameters work across all providers (P2)
- [x] **US-5**: ExtraParameters on embedding requests (P2)

## Constitution Compliance

- [x] Unified Abstraction — ExtraParameters available on all request types; merge at LlmHttpClient layer
- [x] No Exceptions for API Errors — ArgumentException only for programming errors (non-object JSON)
- [x] Debuggability First — This IS the debuggability feature (RawRequestJson, RawResponseJson, ExtraParameters)
- [x] Immutability — ExtraParameters is JsonElement? on immutable records; JsonDeepMerge is stateless
- [x] Test-Driven Quality — 41 tests across 6 test classes
- [x] Multi-Target Compatibility — System.Text.Json on net8.0;net10.0
- [x] Documentation as Deliverable — wiki/getting-started.md, wiki/runtime-configuration.md

## Documentation

- [x] `wiki/getting-started.md` — ExtraParameters overview
- [x] `wiki/runtime-configuration.md` — ExtraParameters override examples
- [x] `wiki/provider-features.md` — ExtraParameters in notes
