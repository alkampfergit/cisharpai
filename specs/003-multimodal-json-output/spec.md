# Feature Specification: Multimodal Images & JSON Output

**Feature Branch**: `feature/gh-specify`

**Created**: 2026-05-14

**Status**: Complete (Retrospec)

**Input**: User description: "now retrospect the feature that allows to use images into the chats, as well as json output support"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Send Images in Chat Messages (Priority: P1)

As a developer, I want to include images alongside text in chat messages so that I can build applications where the LLM analyzes visual content (photos, screenshots, diagrams, documents).

**Why this priority**: Vision/multimodal input is the core value proposition — without it, applications cannot leverage the model's visual understanding capabilities.

**Independent Test**: Create a message with a text prompt and a base64-encoded image, send it via the standard `GetChatCompletionAsync`, verify the response contains a text description of the image.

**Acceptance Scenarios**:

1. **Given** a message created with `LlmMessage.WithImage(text, filePath)`, **When** sent to an OpenAI-compatible provider, **Then** the response contains a text description of the image and `IsSuccess == true`.
2. **Given** a message created with `LlmMessage.WithBase64Image(text, base64Data, mediaType)`, **When** sent to any vision-capable provider, **Then** the image is correctly encoded in the provider's native format.
3. **Given** a message with multiple image content parts, **When** sent to a provider, **Then** all images are included in the request and the model references all of them.

---

### User Story 2 - Enforce JSON Mode Output (Priority: P1)

As a developer, I want to force the model to produce valid JSON output so that I can reliably parse the response without worrying about markdown wrapping or natural-language preamble.

**Why this priority**: JSON mode is the foundational structured output capability — it works across all providers and model families.

**Independent Test**: Send a request via `IJsonOutputFeature` with `JsonOutputMode.JsonMode`, verify the response content is valid JSON.

**Acceptance Scenarios**:

1. **Given** `JsonOutputMode.JsonMode` and a user prompt, **When** the request is sent, **Then** the response content is parseable as valid JSON.
2. **Given** a system message that does not contain the word "JSON", **When** using JsonMode, **Then** the library automatically appends "Respond in JSON." to the system message.
3. **Given** a system message that already contains the word "JSON", **When** using JsonMode, **Then** the system message is not modified.

---

### User Story 3 - Enforce Structured Outputs with JSON Schema (Priority: P1)

As a developer, I want to provide a JSON Schema and have the model produce output conforming to it so that I can get predictable, type-safe responses without post-processing.

**Why this priority**: Structured Outputs with schema enforcement is the highest-fidelity way to get structured data from LLMs.

**Independent Test**: Send a request via `IJsonOutputFeature` with `JsonOutputMode.JsonSchema`, a schema name, and a JSON Schema string, verify the response content conforms to the schema.

**Acceptance Scenarios**:

1. **Given** `JsonOutputMode.JsonSchema` with a valid schema name and JSON Schema, **When** the request is sent, **Then** the response content matches the provided schema.
2. **Given** a Structured Outputs request, **When** the model refuses for safety reasons, **Then** `response.Refusal` is non-null and content may be empty.
3. **Given** `Strict == true` (default), **When** sent to OpenAI, **Then** the provider enforces strict schema adherence via its Structured Outputs engine.

---

### User Story 4 - Use Multiple Content Part Types in a Message (Priority: P2)

As a developer, I want to compose messages from mixed content parts (text, file images, base64 images) so that I can build flexible multimodal interactions.

**Why this priority**: Important for production applications that need to send multiple images or mix image sources, but single-image convenience methods cover most cases.

**Independent Test**: Create a message with `ContentParts` containing a `TextContentPart`, an `ImageFileContentPart`, and an `ImageBase64ContentPart`, send it, verify all parts are included in the request.

**Acceptance Scenarios**:

1. **Given** a `LlmMessage` with a `ContentParts` list, **When** the provider builds the request, **Then** each content part is mapped to the provider's native format.
2. **Given** an `ImageFileContentPart` with a local file path, **When** the request is built, **Then** the file is read from disk and converted to a data URI (or provider-native format).
3. **Given** an `ImageBase64ContentPart`, **When** sent to OpenAI, **Then** it becomes a data URI; **When** sent to Anthropic, **Then** it becomes a base64 `source` block.

---

### User Story 5 - Validate JSON Output Configuration (Priority: P2)

As a developer, I want invalid JSON output options to be caught early so that I get clear error messages instead of cryptic provider API errors.

**Why this priority**: Validation prevents wasted API calls and provides a better developer experience.

**Independent Test**: Create `JsonOutputOptions` with `JsonSchema` mode but missing schema name, call `Validate()`, verify `ArgumentException` is thrown.

**Acceptance Scenarios**:

1. **Given** `JsonOutputMode.JsonSchema` without a `SchemaName`, **When** `Validate()` is called, **Then** it throws `ArgumentException`.
2. **Given** `JsonOutputMode.JsonSchema` without a `JsonSchema`, **When** `Validate()` is called, **Then** it throws `ArgumentException`.
3. **Given** `JsonOutputMode.JsonSchema` with invalid JSON in `JsonSchema`, **When** `Validate()` is called, **Then** it throws `ArgumentException` with the parse error message.
4. **Given** `JsonOutputMode.JsonMode` without any schema fields, **When** `Validate()` is called, **Then** it succeeds (no validation needed).

---

### User Story 6 - Handle Provider-Specific Image Formats Transparently (Priority: P2)

As a developer, I want the library to automatically convert my images to each provider's native format so that I can use the same message across different providers.

**Why this priority**: Transparent format conversion is the core abstraction value — without it, callers need provider-specific code.

**Independent Test**: Send the same `LlmMessage.WithBase64Image(...)` to OpenAI (expects data URI) and Anthropic (expects base64 source block), verify both produce correct responses.

**Acceptance Scenarios**:

1. **Given** an image message sent to OpenAI/Azure, **Then** the image is encoded as `data:{mime};base64,{data}` in an `image_url` content part.
2. **Given** an image message sent to Anthropic, **Then** the image is encoded as a `source` block with `type: "base64"`, `media_type`, and raw `data`.
3. **Given** an image message sent to Cohere, **Then** image parts are silently skipped and only text content is sent (Cohere does not support chat vision).

---

### User Story 7 - Test JSON Output with Fakes (Priority: P2)

As a developer writing unit tests, I want to use the fake client to simulate JSON output responses so that I can test my JSON-handling logic without real API calls.

**Why this priority**: Testing support is essential for production adoption.

**Independent Test**: Configure `FakeChatCompletionClient` with an enqueued response, call `GetChatCompletionWithJsonOutputAsync`, verify the response is returned and the request is captured.

**Acceptance Scenarios**:

1. **Given** a `FakeChatCompletionClient` with an enqueued response, **When** I call `GetChatCompletionWithJsonOutputAsync`, **Then** the enqueued response is returned and the request is captured in `ReceivedJsonOutputRequests`.
2. **Given** `FakeChatFeatures.JsonOutput` is not set, **When** I call `Features.Get<IJsonOutputFeature>()`, **Then** it returns null.

---

### Edge Cases

- What happens when a message has `ContentParts` but the provider doesn't support images? → Cohere silently skips image parts and sends only text.
- What happens when an `ImageFileContentPart` points to a non-existent file? → `File.ReadAllBytesAsync` throws `FileNotFoundException` (I/O error, not API error).
- What happens when the system message already contains "JSON"? → `JsonOutputHelper.EnsureJsonKeywordInSystemMessage` leaves it unchanged.
- What happens when the model wraps JSON in markdown code fences? → `JsonOutputHelper.StripMarkdownCodeFences` removes them.
- What happens when `JsonSchema` string contains invalid JSON? → `Validate()` throws `ArgumentException` with parse details.
- What happens with an unsupported image format extension? → `ImageDataUriHelper.GetMimeType` returns `application/octet-stream`.
- What happens when the model refuses a Structured Outputs request? → `response.Refusal` is populated; content may be empty.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support multimodal messages containing text and images in the same message.
- **FR-002**: System MUST provide three content part types: text, file-based image, and base64-encoded image.
- **FR-003**: System MUST provide convenience factory methods for creating single-image messages (`WithImage`, `WithBase64Image`).
- **FR-004**: System MUST automatically convert image file paths to the provider's native image format (data URI or base64 source block).
- **FR-005**: System MUST detect image MIME types from file extensions (PNG, JPEG, WebP, GIF).
- **FR-006**: System MUST silently skip image content parts for providers that do not support chat vision (Cohere).
- **FR-007**: System MUST provide an `IJsonOutputFeature` interface discoverable via the Feature Collection pattern.
- **FR-008**: The feature MUST accept a `ChatCompletionRequest` plus `JsonOutputOptions` and return a `ChatCompletionResponse`.
- **FR-009**: System MUST support two JSON output modes: JsonMode (json_object) and JsonSchema (json_schema with strict enforcement).
- **FR-010**: System MUST automatically inject the word "JSON" into the system message when using JsonMode and it is not already present.
- **FR-011**: System MUST strip markdown code fences from JSON output when the provider may wrap responses in code blocks.
- **FR-012**: System MUST validate `JsonOutputOptions` before making API calls, throwing `ArgumentException` for missing schema name, missing schema, or invalid JSON.
- **FR-013**: System MUST implement JSON output for all 5 providers: OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere.
- **FR-014**: System MUST implement multimodal messages for all 5 providers (with graceful degradation for Cohere).
- **FR-015**: System MUST map provider-specific response format and image formats transparently.
- **FR-016**: System MUST surface Structured Outputs safety refusals via the `Refusal` property on the response.
- **FR-017**: System MUST return API errors as `ChatCompletionResponse.Error()` without throwing exceptions.
- **FR-018**: System MUST provide fake client support for queuing, defaulting, and capturing JSON output requests.
- **FR-019**: OpenAI provider MUST support both Chat Completions API (`response_format`) and Responses API (`text.format`) for JSON output.
- **FR-020**: `JsonOutputOptions.Strict` MUST default to `true` and control whether strict schema enforcement is requested.

### Key Entities

- **MessageContentPart**: Base type for discriminated-union content parts — text, file image, or base64 image.
- **JsonOutputOptions**: Configuration for JSON output — mode, schema name, schema description, JSON schema string, strict flag.
- **JsonOutputMode**: Enum controlling output mode — JsonMode (valid JSON) vs JsonSchema (schema-conforming JSON).
- **IJsonOutputFeature**: Optional feature interface for JSON output enforcement.
- **ImageDataUriHelper**: Utility for converting file paths to data URIs and detecting MIME types.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 5 providers pass unit tests verifying JSON output mode mapping (JsonMode → json_object, JsonSchema → json_schema).
- **SC-002**: All 4 vision-capable providers pass unit tests verifying content part mapping (text + images in correct provider format).
- **SC-003**: Cohere correctly degrades by extracting text-only content from multimodal messages.
- **SC-004**: Invalid `JsonOutputOptions` are rejected at validation time with descriptive error messages.
- **SC-005**: System message auto-injection of "JSON" keyword is tested for both present and absent cases.
- **SC-006**: Markdown code fence stripping correctly handles `json`, plain, and no-fence content.
- **SC-007**: `FakeChatCompletionClient` supports queuing and capturing JSON output requests.
- **SC-008**: JSON output integration tests pass for all 5 providers against real APIs.
- **SC-009**: File-to-data-URI conversion produces correct MIME types for PNG, JPEG, WebP, and GIF.

## Assumptions

- Multimodal images and JSON output build on the base `IChatCompletionClient` infrastructure (spec 001).
- All providers support JSON Mode (json_object) at minimum; Structured Outputs (json_schema) support varies by model.
- Image input is supported by OpenAI, Azure OpenAI, Azure AI Inference, and Anthropic; Cohere only supports images via embeddings.
- Anthropic's image format (base64 source blocks) is fundamentally different from the OpenAI-style data URI format, requiring separate mapping.
- JSON Schema construction and correctness is the caller's responsibility; the library only validates that the string is parseable JSON.
- The "JSON" keyword injection for JsonMode is required by OpenAI-compatible providers; it is harmless for others.

## Retrospec Metadata

**Generated**: 2026-05-14
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 43+ files across 8 projects
**Reference implementation branch**: feature/gh-specify
