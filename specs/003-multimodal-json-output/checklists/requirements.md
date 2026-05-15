# Requirements Checklist: Multimodal Images & JSON Output

All items marked `[x]` — implementation verified against existing codebase.

## Functional Requirements

- [x] **FR-001**: Multimodal messages containing text and images in the same message
  → `LlmMessage.ContentParts` property; `MessageContentPart` hierarchy in `src/Cisharpai/Models/MessageContentPart.cs`

- [x] **FR-002**: Three content part types: text, file-based image, base64-encoded image
  → `TextContentPart`, `ImageFileContentPart`, `ImageBase64ContentPart` in `src/Cisharpai/Models/MessageContentPart.cs`

- [x] **FR-003**: Convenience factory methods `WithImage` and `WithBase64Image`
  → `LlmMessage.WithImage()` and `LlmMessage.WithBase64Image()` in `src/Cisharpai/Models/LlmMessage.cs`

- [x] **FR-004**: Automatic image file-to-provider-format conversion
  → `ImageDataUriHelper.ToDataUriAsync()` + `ContentPartHelper.MapOpenAiStyleContentPartsAsync()` for OpenAI/Azure; custom Anthropic mapping

- [x] **FR-005**: MIME type detection from file extensions (PNG, JPEG, WebP, GIF)
  → `ImageDataUriHelper.GetMimeType()` in `src/Cisharpai/ImageDataUriHelper.cs`

- [x] **FR-006**: Silent image part skipping for non-vision providers (Cohere)
  → `CohereChatCompletionClient` extracts text-only content from ContentParts

- [x] **FR-007**: `IJsonOutputFeature` interface discoverable via Feature Collection
  → `src/Cisharpai/Features/Chat/IJsonOutputFeature.cs`; all 5 providers register it

- [x] **FR-008**: Feature accepts `ChatCompletionRequest` + `JsonOutputOptions` and returns `ChatCompletionResponse`
  → `GetChatCompletionWithJsonOutputAsync(ChatCompletionRequest, JsonOutputOptions, CancellationToken)`

- [x] **FR-009**: Two JSON output modes: JsonMode and JsonSchema
  → `src/Cisharpai/Models/JsonOutputMode.cs` enum with `JsonMode` and `JsonSchema` values

- [x] **FR-010**: Automatic "JSON" keyword injection into system message for JsonMode
  → `JsonOutputHelper.EnsureJsonKeywordInSystemMessage()` in `src/Cisharpai/Helpers/JsonOutputHelper.cs`

- [x] **FR-011**: Markdown code fence stripping for providers that wrap JSON output
  → `JsonOutputHelper.StripMarkdownCodeFences()` in `src/Cisharpai/Helpers/JsonOutputHelper.cs`

- [x] **FR-012**: Validation of `JsonOutputOptions` before API calls
  → `JsonOutputOptions.Validate()` throws `ArgumentException` for missing schema name, missing schema, or invalid JSON

- [x] **FR-013**: JSON output implemented for all 5 providers
  → `OpenAiChatCompletionClient`, `AzureOpenAiChatCompletionClient`, `AzureAiInferenceChatCompletionClient`, `AnthropicChatCompletionClient`, `CohereChatCompletionClient`

- [x] **FR-014**: Multimodal messages for all 5 providers (Cohere: graceful degradation)
  → All 5 provider clients handle `ContentParts`; Cohere extracts text only

- [x] **FR-015**: Provider-specific response format and image format mapping
  → `OpenAiResponseFormat`, `AzureOpenAiResponseFormat`, `AzureAiInferenceResponseFormat`, `AnthropicOutputConfig`, `CohereChatResponseFormat`; `OpenAiContentPart`, `AzureOpenAiContentPart`, `AzureAiInferenceContentPart`, `AnthropicImageSource`

- [x] **FR-016**: Structured Outputs safety refusals surfaced via `Refusal` property
  → `ChatCompletionResponse.Refusal` populated by OpenAI (Responses API refusal), Anthropic (stop_reason="refusal"), Azure AI Inference

- [x] **FR-017**: API errors returned as `ChatCompletionResponse.Error()` without exceptions
  → All providers catch `LlmHttpRequestException` and return error responses

- [x] **FR-018**: Fake client support for JSON output (queue, default, capture)
  → `FakeChatCompletionClient`: `EnqueueJsonOutputResponse()`, `DefaultJsonOutputResponse`, `ReceivedJsonOutputRequests`

- [x] **FR-019**: OpenAI supports both Chat Completions API and Responses API for JSON output
  → `OpenAiResponseFormat` (Chat Completions) + `OpenAiTextFormat` (Responses API / GPT-5) in `src/Cisharpai.OpenAi/Models/OpenAiResponseFormat.cs`

- [x] **FR-020**: `JsonOutputOptions.Strict` defaults to `true`
  → `bool Strict = true` in `JsonOutputOptions` record definition

## User Stories

- [x] **US-1**: Send images in chat messages — `LlmMessage.WithImage/WithBase64Image`, ContentParts
- [x] **US-2**: Enforce JSON Mode output — `JsonOutputMode.JsonMode` with auto system message injection
- [x] **US-3**: Enforce Structured Outputs with JSON Schema — `JsonOutputMode.JsonSchema` with name + schema
- [x] **US-4**: Multiple content part types — manual ContentParts composition with mixed types
- [x] **US-5**: Validate JSON output configuration — `JsonOutputOptions.Validate()` catches missing/invalid inputs
- [x] **US-6**: Provider-specific image formats — transparent conversion (data URI for OpenAI, base64 source for Anthropic, skip for Cohere)
- [x] **US-7**: Test JSON output with fakes — `FakeChatCompletionClient` queue/default/capture

## Constitution Compliance

- [x] **I. Unified Abstraction** — `IJsonOutputFeature` via Feature Collection; `MessageContentPart` works cross-provider
- [x] **II. No Exceptions for API Errors** — `ChatCompletionResponse.Error()` for API failures
- [x] **III. Debuggability First** — inherits `IncludeRawResponse`/`ExtraParameters` from base request
- [x] **IV. Immutability** — all DTOs are sealed records (`JsonOutputOptions`, `MessageContentPart` hierarchy, `JsonOutputMode`)
- [x] **V. Test-Driven Quality** — unit tests for all providers (JSON output + vision) + model validation + fakes
- [x] **VI. Multi-Target Compatibility** — all projects target net8.0;net10.0
- [x] **VII. Documentation as Deliverable** — `wiki/json-output.md` and `wiki/vision.md` complete

## Test Coverage

- [x] Unit tests: JsonOutputOptions validation (`JsonOutputOptionsTests`)
- [x] Unit tests: MessageContentPart hierarchy (`MessageContentPartTests`)
- [x] Unit tests: ImageDataUriHelper (`ImageDataUriHelperTests`)
- [x] Unit tests: OpenAI JSON output (`OpenAiJsonOutputTests`)
- [x] Unit tests: OpenAI vision (`OpenAiVisionTests`)
- [x] Unit tests: Azure OpenAI JSON output (`AzureOpenAiJsonOutputTests`)
- [x] Unit tests: Azure OpenAI vision (`AzureOpenAiVisionTests`)
- [x] Unit tests: Azure AI Inference JSON output (`AzureAiInferenceJsonOutputTests`)
- [x] Unit tests: Azure AI Inference vision (`AzureAiInferenceVisionTests`)
- [x] Unit tests: Anthropic JSON output (`AnthropicJsonOutputTests`)
- [x] Unit tests: Anthropic vision (`AnthropicVisionTests`)
- [x] Unit tests: Cohere JSON output (`CohereJsonOutputTests`)
- [x] Unit tests: Cohere vision (`CohereVisionTests`)
- [x] Unit tests: FakeChatCompletionClient JSON output (`FakeChatCompletionClientTests`)
- [x] Integration tests: OpenAI JSON output (`OpenAiJsonOutputIntegrationTests`)
- [x] Integration tests: Azure OpenAI JSON output (`AzureOpenAiJsonOutputIntegrationTests`)
- [x] Integration tests: Azure AI Inference JSON output (`AzureAiInferenceJsonOutputIntegrationTests`)
- [x] Integration tests: Anthropic JSON output (`AnthropicJsonOutputIntegrationTests`)
- [x] Integration tests: Cohere JSON output (`CohereJsonOutputIntegrationTests`)
