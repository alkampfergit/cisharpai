# Requirements Checklist: Standard Chat Completion

All items marked `[x]` — implementation verified against existing codebase.

## Functional Requirements

- [x] **FR-001**: Single `IChatCompletionClient` interface with `GetChatCompletionAsync`
  → `src/Cisharpai/IChatCompletionClient.cs`

- [x] **FR-002**: Implemented for OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere
  → `OpenAiChatCompletionClient`, `AzureOpenAiChatCompletionClient`, `AzureAiInferenceChatCompletionClient`, `AnthropicChatCompletionClient`, `CohereChatCompletionClient`

- [x] **FR-003**: Four message roles: System, User, Assistant, Tool
  → `src/Cisharpai/Models/LlmRole.cs`

- [x] **FR-004**: Model selection per-request or via DefaultModel
  → Each provider's `ResolveModel()` method falls back to options.DefaultModel

- [x] **FR-005**: Temperature and MaxTokens parameters
  → `ChatCompletionRequest` record fields; mapped in all provider clients

- [x] **FR-006**: Token usage in every response
  → `ChatCompletionResponse.PromptTokens` / `CompletionTokens` populated by all providers

- [x] **FR-007**: API errors returned as `IsSuccess=false` without exceptions
  → All providers catch `LlmHttpRequestException` and return `ChatCompletionResponse.Error(...)`

- [x] **FR-008**: Raw JSON via `IncludeRawResponse`
  → `PostWithRawAsync` path in `LlmHttpClient`; all providers conditionally call it

- [x] **FR-009**: ExtraParameters deep-merge
  → `JsonDeepMerge.Merge()` called in `LlmHttpClient.SerializeAndMerge()`

- [x] **FR-010**: DI extension methods with auth handlers and resilience
  → `AddOpenAiClient`, `AddAzureOpenAiClient`, `AddAzureAiInferenceClient`, `AddAnthropicClient`, `AddCohereChatClient` + `AddCisharpaiResilienceHandler`

- [x] **FR-011**: Keyed DI registration
  → All DI extensions have `(string key, ...)` overloads using `AddKeyedSingleton` / `AddKeyedTransient`

- [x] **FR-012**: Static `Create` factory methods
  → All 5 provider clients have `public static XxxClient Create(IHttpMessageHandlerFactory, options, ...)` methods

- [x] **FR-013**: Feature Collection pattern
  → `IHasFeatures`, `IFeatureCollection`, `FeatureCollection` in `src/Cisharpai/Features/`

- [x] **FR-014**: Provider-specific message format mapping
  → Anthropic: system message extracted to top-level `system` field; Cohere: snake_case serialization; all providers: role mapping via `RoleMapper` or custom

- [x] **FR-015**: Multimodal messages via ContentParts
  → `MessageContentPart` hierarchy; `ContentPartHelper.MapOpenAiStyleContentPartsAsync` for OpenAI/Azure; custom mapping in Anthropic; text-only fallback in Cohere

- [x] **FR-016**: Automatic model-type routing
  → `DetectModelType` in OpenAI (legacy/reasoning/GPT-5); `DetectModelTypeForRequest` with routing fallback in Azure OpenAI; `IsReasoningModel` in Azure AI Inference

- [x] **FR-017**: OpenTelemetry-compatible Activity spans
  → `CisharpaiTelemetry.ActivitySource` used in `LlmHttpClient.StartHttpActivity()`

- [x] **FR-018**: Structured logging with event IDs
  → `LoggerMessage.Define` in `LlmHttpClient` (EventId 1000–1005)

- [x] **FR-019**: FakeChatCompletionClient for testing
  → `src/Cisharpai.Testing/FakeChatCompletionClient.cs` with queue, defaults, and request capture

- [x] **FR-020**: Refusal field handling
  → `ChatCompletionResponse.Refusal` populated by OpenAI (Responses API refusal content), Anthropic (stop_reason="refusal"), Azure AI Inference (choice.Message.Refusal)

## User Stories

- [x] **US-1**: Send chat message via any provider — all 5 providers implement `GetChatCompletionAsync`
- [x] **US-2**: Configure via DI — extension methods for all 5 providers
- [x] **US-3**: Inspect raw JSON — `IncludeRawResponse` path in all providers
- [x] **US-4**: ExtraParameters escape hatch — deep-merge via `JsonDeepMerge`
- [x] **US-5**: Error handling without exceptions — `ChatCompletionResponse.Error()` pattern
- [x] **US-6**: Multi-turn conversation — `LlmMessage` with System/User/Assistant roles
- [x] **US-7**: Runtime configuration — `Create` factory methods on all providers
- [x] **US-8**: Feature discovery — `IHasFeatures` / `IFeatureCollection` on all clients

## Constitution Compliance

- [x] **I. Unified Abstraction** — single interface, Feature Collection pattern
- [x] **II. No Exceptions for API Errors** — all providers return error responses
- [x] **III. Debuggability First** — RawResponseJson/RawRequestJson, ExtraParameters
- [x] **IV. Immutability** — sealed records for all DTOs
- [x] **V. Test-Driven Quality** — unit tests for all providers + DI + fakes
- [x] **VI. Multi-Target Compatibility** — net8.0;net10.0 in all projects
- [x] **VII. Documentation as Deliverable** — wiki/getting-started.md, wiki/provider-features.md

## Test Coverage

- [x] Unit tests for all 5 provider chat completion clients
- [x] Unit tests for core `ChatCompletionResponse` error factory
- [x] Unit tests for DI registration (all 5 providers)
- [x] Unit tests for `FakeChatCompletionClient`
- [x] Integration tests for all 5 providers (chat completion happy path)
