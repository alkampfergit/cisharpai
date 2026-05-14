# Requirements Checklist: Grounded Chat

## Functional Requirements

- [x] **FR-001**: Grounded chat via `IGroundedChatFeature` discoverable through Feature Collection
  - Implemented: `IGroundedChatFeature` registered by `CohereChatCompletionClient`; test: `Features_GetGroundedChatFeature_ReturnsSelf`
- [x] **FR-002**: Documents as structured key-value or plain text (mutually exclusive)
  - Verified: `DocumentChunkTests` (8 tests), `GroundedChat_MapsKeyValueDocumentCorrectly`, `GroundedChat_MapsPlainTextDocumentCorrectly`
- [x] **FR-003**: Three citation modes (Accurate, Fast, Enabled)
  - Verified: `GroundedChat_SetsCitationOptionsMode_Accurate`, `_Fast`, `_Enabled`
- [x] **FR-004**: Default citation mode is `Fast`
  - Verified: `GroundedChatOptionsTests.DefaultCitationMode_IsFast`, `GroundedChat_DefaultCitationMode_SendsFast`
- [x] **FR-005**: Silent downgrade of Accurate to Fast on command-a models
  - Verified: `GroundedChat_DowngradesAccurateToFast_OnCommandAModel`
- [x] **FR-006**: Citations with character offsets, text, and source references
  - Verified: `GroundedChat_MapsCitationsFromResponse`, `_MapsCitationSources`, `_MapsCitationType`
- [x] **FR-007**: Document validation before sending
  - Verified: `GroundedChatOptionsTests` (8 tests), `GroundedChat_EmptyDocuments_ReturnsError`, `GroundedChat_InvalidDocument_ReturnsError`
- [x] **FR-008**: No exceptions for API errors
  - Verified: `GroundedChat_HttpError_ReturnsError`
- [x] **FR-009**: Raw JSON capture via `IncludeRawResponse`
  - Verified: `GroundedChat_WithRawResponse_IncludesRawJson`
- [x] **FR-010**: ExtraParameters deep-merge
  - Implemented: Passes `request.ExtraParameters` to `LlmHttpClient.PostAsync`
- [x] **FR-011**: No `response_format` on grounded chat requests
  - Verified: `GroundedChat_DoesNotSetResponseFormat`

## User Stories

- [x] **US-1**: Ask questions grounded on documents (P1)
- [x] **US-2**: Control citation generation mode (P1)
- [x] **US-3**: Receive and navigate citations (P1)
- [x] **US-4**: Handle grounded chat errors (P2)
- [x] **US-5**: Validate document inputs (P2)

## Constitution Compliance

- [x] Unified Abstraction — Feature Collection pattern
- [x] No Exceptions for API Errors — error responses returned
- [x] Debuggability First — raw JSON + ExtraParameters
- [x] Immutability — all DTOs are immutable records
- [x] Test-Driven Quality — 36 unit tests across 3 classes
- [x] Multi-Target Compatibility — net8.0;net10
- [x] Documentation as Deliverable — `wiki/grounded-chat.md`

## Documentation

- [x] `wiki/grounded-chat.md` — comprehensive guide with examples
- [x] `wiki/provider-features.md` — grounded chat in feature matrix
- [x] `memories/project_overview.md` — grounded chat documented
