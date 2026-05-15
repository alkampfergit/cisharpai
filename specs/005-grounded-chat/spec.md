# Feature Specification: Grounded Chat (RAG with Document Citations)

**Feature Branch**: `feature/gh-specify`

**Created**: 2026-05-14

**Status**: Complete (Retrospec)

**Input**: User description: "grounded RAG chat"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask Questions Grounded on Documents (Priority: P1)

As a developer, I want to send a chat request grounded on a set of documents so that the model answers strictly from the provided content and I can build knowledge-base Q&A applications with minimal hallucination.

**Why this priority**: This is the core value proposition — without document grounding, there is no RAG feature.

**Independent Test**: Send a grounded chat request with two structured documents and a question, verify the response contains relevant content and citations pointing back to source documents.

**Acceptance Scenarios**:

1. **Given** a Cohere chat client and two documents with structured key-value data, **When** I call `GetGroundedChatCompletionAsync`, **Then** the provider receives a `documents` array with correct `id` and `data` fields, and `citation_options` with the chosen mode.
2. **Given** documents with plain text content, **When** sent via grounded chat, **Then** the provider serializes the `data` field as a JSON string (not object).
3. **Given** a document without an `Id`, **When** serialized, **Then** the `id` field is omitted from the provider request.
4. **Given** the response contains citations, **When** mapped to the unified response, **Then** each `Citation` has `Start`, `End`, `Text`, and a list of `Sources` with `Id` and optional `Data`.

---

### User Story 2 - Control Citation Generation Mode (Priority: P1)

As a developer, I want to choose between accurate, fast, or provider-default citation modes so that I can balance citation precision against latency.

**Why this priority**: Citation mode affects both quality and latency — developers need control for different use cases.

**Independent Test**: Send requests with each citation mode and verify the correct mode string appears in the provider request.

**Acceptance Scenarios**:

1. **Given** `CitationMode.Fast`, **When** serialized, **Then** the provider receives `citation_options.mode: "FAST"`.
2. **Given** `CitationMode.Accurate` with a `command-r` model, **When** serialized, **Then** the provider receives `citation_options.mode: "ACCURATE"`.
3. **Given** `CitationMode.Enabled`, **When** serialized, **Then** the provider receives `citation_options.mode: "ENABLED"`.
4. **Given** no explicit citation mode, **When** using the default, **Then** the default is `Fast`.
5. **Given** `CitationMode.Accurate` with a `command-a` model, **When** the request is processed, **Then** the mode is silently downgraded to `Fast` (with a log warning) because `command-a` does not support `Accurate`.

---

### User Story 3 - Receive and Navigate Citations (Priority: P1)

As a developer, I want the response to include character-offset citations pointing to source documents so that I can highlight cited spans in the UI and link them to their sources.

**Why this priority**: Citations are what distinguish grounded chat from regular chat — they are the proof of grounding.

**Independent Test**: Parse a response with citations and verify offsets, text, sources, and optional type are correctly mapped.

**Acceptance Scenarios**:

1. **Given** a response with one citation, **Then** `Citations[0].Start`, `End`, and `Text` match the provider's values, and `Sources[0].Id` and `Sources[0].Data` are populated.
2. **Given** a response with multiple citations, **Then** all are mapped to the unified response.
3. **Given** a response with no citations, **Then** `Citations` is an empty list (not null).
4. **Given** a citation without a `type` field, **Then** `Citation.Type` is null.
5. **Given** a citation with `type: "TEXT_CONTENT"`, **Then** `Citation.Type` equals `"TEXT_CONTENT"`.

---

### User Story 4 - Handle Grounded Chat Errors (Priority: P2)

As a developer, I want grounded chat errors returned as failed responses (not exceptions) so that I handle them via property checks, consistent with all other Cisharpai operations.

**Why this priority**: No-exceptions-for-API-errors is a non-negotiable project principle.

**Independent Test**: Trigger an HTTP 500 and verify `IsSuccess == false` with empty citations.

**Acceptance Scenarios**:

1. **Given** the provider returns HTTP 500, **When** a grounded chat request is made, **Then** `response.IsSuccess` is `false`, `response.ErrorMessage` is populated, and `response.Citations` is empty.
2. **Given** an empty documents list, **When** `GetGroundedChatCompletionAsync` is called, **Then** the response has `IsSuccess == false` (validation catches it before sending).
3. **Given** a document with neither `Data` nor `Text`, **When** the request is made, **Then** `IsSuccess == false`.

---

### User Story 5 - Validate Document Inputs (Priority: P2)

As a developer, I want clear validation errors when document inputs are malformed so that I catch configuration mistakes early.

**Why this priority**: Validation prevents confusing API errors from the provider and gives developers actionable error messages.

**Independent Test**: Create documents violating constraints and verify `ArgumentException` is thrown from `Validate()`.

**Acceptance Scenarios**:

1. **Given** a `DocumentChunk` with both `Data` and `Text` set, **When** `Validate()` is called, **Then** it throws `ArgumentException`.
2. **Given** a `DocumentChunk` with neither `Data` nor `Text`, **When** `Validate()` is called, **Then** it throws `ArgumentException`.
3. **Given** a `DocumentChunk` with empty `Data` dictionary, **When** `Validate()` is called, **Then** it throws `ArgumentException`.
4. **Given** a `DocumentChunk` with whitespace-only `Text`, **When** `Validate()` is called, **Then** it throws `ArgumentException`.
5. **Given** a `GroundedChatOptions` with an empty `Documents` list, **When** `Validate()` is called, **Then** it throws `ArgumentException`.
6. **Given** a `GroundedChatOptions` where one document is invalid among valid ones, **When** `Validate()` is called, **Then** it throws `ArgumentException`.

---

### Edge Cases

- What happens when the model doesn't find relevant content in documents? (Returns content with empty citations list)
- What happens when `CitationMode.Accurate` is requested for a `command-a` model? (Silently downgrades to `Fast` with a log warning)
- Is grounded chat compatible with JSON Mode? (No — Cohere API rejects `documents` + `response_format` together; no `response_format` is set for grounded requests)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support document-grounded chat completions via an optional `IGroundedChatFeature` discoverable through the Feature Collection pattern.
- **FR-002**: System MUST accept documents as either structured key-value pairs or plain text, but not both simultaneously.
- **FR-003**: System MUST support three citation modes: Accurate, Fast, and Enabled.
- **FR-004**: System MUST default to `Fast` citation mode, which is supported by all Cohere model families.
- **FR-005**: System MUST silently downgrade `Accurate` to `Fast` (with a log warning) when the model is a `command-a` variant that does not support `Accurate`.
- **FR-006**: System MUST return citations with character offsets (start/end), cited text, and source document references.
- **FR-007**: System MUST validate documents before sending (at least one document required; each has exactly one of Data or Text).
- **FR-008**: System MUST return `IsSuccess=false` for API errors (no exceptions).
- **FR-009**: System MUST support raw request/response JSON capture via `IncludeRawResponse`.
- **FR-010**: System MUST support `ExtraParameters` deep-merge for grounded chat requests.
- **FR-011**: System MUST NOT set `response_format` on grounded chat requests (mutually exclusive with JSON Mode).

### Key Entities

- **GroundedChatOptions**: Configuration — documents list and citation mode.
- **DocumentChunk**: A single document input — optional ID, mutually exclusive Data (key-value) or Text.
- **GroundedChatCompletionResponse**: Wraps `ChatCompletionResponse` with a list of citations; convenience properties for `IsSuccess`, `Content`, `ErrorMessage`.
- **Citation**: A cited span — start/end offsets, text, list of sources, optional type.
- **CitationSource**: A source reference — document ID and optional data dictionary.
- **CitationMode**: Enum controlling citation precision/latency tradeoff.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Grounded chat works on Cohere; feature discovery returns null on all other providers.
- **SC-002**: All three citation modes produce correct provider-level serialization.
- **SC-003**: `command-a` models receive `FAST` even when `Accurate` is requested.
- **SC-004**: Citations are correctly mapped with offsets, text, and sources.
- **SC-005**: All validation edge cases are caught before the HTTP call.
- **SC-006**: All unit tests pass on both .NET 8.0 and .NET 10.

## Assumptions

- Cohere is the only provider supporting document-grounded chat with citations.
- The Cohere Chat v2 API is the target endpoint (`/chat` with `documents` array).
- `command-a` models do not support `CitationMode.Accurate`; only `command-r` models do.
- Grounded chat and JSON Mode are mutually exclusive at the Cohere API level.

## Retrospec Metadata

**Generated**: 2026-05-14
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 13 files across 4 projects
**Reference implementation branch**: feature/gh-specify
