# Requirements Checklist: Tool Calling

All items marked `[x]` — implementation verified against existing codebase.

## Functional Requirements

- [x] **FR-001**: `IToolCallingFeature` interface discoverable via Feature Collection pattern
  → `src/Cisharpai/Features/Chat/IToolCallingFeature.cs`; all 5 providers register it in their `FeatureCollection`

- [x] **FR-002**: Feature accepts `ChatCompletionRequest` + `ToolCallingOptions` and returns `ToolCallingResponse`
  → `GetChatCompletionWithToolsAsync(ChatCompletionRequest, ToolCallingOptions, CancellationToken)`

- [x] **FR-003**: `ToolCallingResponse` wraps `ChatCompletionResponse` and adds optional `ToolCall` list
  → `src/Cisharpai/Models/ToolCallingResponse.cs` with `ChatCompletion` + `ToolCalls` fields

- [x] **FR-004**: Each `ToolCall` contains provider-assigned ID, function name, and parsed JSON arguments
  → `src/Cisharpai/Models/ToolCall.cs` — `sealed record ToolCall(string Id, string FunctionName, JsonElement Arguments)`

- [x] **FR-005**: Tools defined via `ToolDefinition` with name, description, JSON Schema parameters, and strict flag
  → `src/Cisharpai/Models/ToolDefinition.cs`

- [x] **FR-006**: Four tool selection strategies: Auto, None, Required, Specific with graceful degradation
  → `src/Cisharpai/Models/ToolChoice.cs` — closed hierarchy with Cohere `Specific→Required` degradation

- [x] **FR-007**: Validation before API calls, throwing `ArgumentException` for invalid input
  → `ToolCallingOptions.Validate()` and `ToolDefinition.Validate()`

- [x] **FR-008**: Tool calling implemented for all 5 providers
  → `OpenAiChatCompletionClient`, `AzureOpenAiChatCompletionClient`, `AzureAiInferenceChatCompletionClient`, `AnthropicChatCompletionClient`, `CohereChatCompletionClient`

- [x] **FR-009**: Provider-specific tool call formats mapped transparently
  → `ToolCallingHelper.MapResponseToolCalls<T>` with per-provider extractor lambdas; Anthropic `tool_use` blocks, Cohere `tool_calls`, OpenAI function calls

- [x] **FR-010**: Multi-turn tool conversations via `LlmMessage` with `ToolCallId` and `ToolCalls`
  → `LlmMessage` record supports `ToolCallId` (Tool role) and `ToolCalls` (Assistant role); all providers map these correctly

- [x] **FR-011**: API errors returned as `ToolCallingResponse.Error()` without throwing
  → `ToolCallingResponse.Error(errorMessage, rawResponseJson?)` static factory

- [x] **FR-012**: Streaming tool call deltas via `ToolCallDelta` in `ChatCompletionChunk`
  → `ChatCompletionChunk.ToolCallDelta` field; `ToolCallingHelper.MapStreamToolCallDelta<T>`

- [x] **FR-013**: `ToolResult` record for tool execution outputs
  → `src/Cisharpai/Models/ToolResult.cs` — `sealed record ToolResult(string ToolCallId, string Content, bool IsError = false)`

- [x] **FR-014**: `FakeResponses.ToolCall()` and `FakeResponses.ToolCalls()` factory methods
  → `src/Cisharpai.Testing/FakeResponses.cs` — both static methods present

- [x] **FR-015**: `IncludeRawResponse` and `ExtraParameters` supported on tool calling requests
  → Inherited from `ChatCompletionRequest` which is passed through to the underlying HTTP call

- [x] **FR-016**: Cohere sets `strict_tools: true` when all tools are strict
  → `CohereChatCompletionClient` checks `toolOptions.Tools.All(t => t.Strict)` and sets `StrictTools = true` on wire request

## User Stories

- [x] **US-1**: Invoke tools via chat completion — all 5 providers return `ToolCall` objects with ID, function name, parsed arguments
- [x] **US-2**: Multi-turn tool conversation — `LlmMessage` with Tool role + ToolCallId sends results back to model
- [x] **US-3**: Control tool selection strategy — `ToolChoice.Auto/None/Required/Specific` mapped per provider
- [x] **US-4**: Define tool parameters with JSON Schema — `ToolDefinition.Parameters` validated as JSON object
- [x] **US-5**: Handle errors without exceptions — `ToolCallingResponse.Error()` factory, validation throws only for config errors
- [x] **US-6**: Stream tool call deltas — `ToolCallDelta` in `ChatCompletionChunk` with incremental arguments
- [x] **US-7**: Test with fakes — `FakeChatCompletionClient` queues/defaults/capture for `IToolCallingFeature`

## Constitution Compliance

- [x] **I. Unified Abstraction** — single `IToolCallingFeature` interface, Feature Collection discovery
- [x] **II. No Exceptions for API Errors** — `ToolCallingResponse.Error()` for API failures; `ArgumentException` only for config
- [x] **III. Debuggability First** — inherits `IncludeRawResponse`/`ExtraParameters` from base request
- [x] **IV. Immutability** — all tool calling DTOs are sealed records
- [x] **V. Test-Driven Quality** — unit tests for all 5 providers + model validation + fakes; integration tests for all 5
- [x] **VI. Multi-Target Compatibility** — all projects target net8.0;net10.0
- [x] **VII. Documentation as Deliverable** — `wiki/tool-calling.md` with quick start, provider matrix, and differences

## Test Coverage

- [x] Unit tests: OpenAI tool calling (`OpenAiToolCallingTests`)
- [x] Unit tests: Azure OpenAI tool calling (`AzureOpenAiToolCallingTests`)
- [x] Unit tests: Azure AI Inference tool calling (`AzureAiInferenceToolCallingTests`)
- [x] Unit tests: Anthropic tool calling (`AnthropicToolCallingTests`)
- [x] Unit tests: Cohere tool calling (`CohereToolCallingTests`)
- [x] Unit tests: ToolDefinition validation (`ToolDefinitionTests`)
- [x] Unit tests: ToolCallingOptions validation (`ToolCallingOptionsTests`)
- [x] Unit tests: ToolCallingResponse factory (`ToolCallingResponseTests`)
- [x] Unit tests: FakeChatCompletionClient tool calling (`FakeChatCompletionClientTests`)
- [x] Unit tests: Feature discovery (`FeatureDiscoveryTests`)
- [x] Integration tests: All 5 providers (`*ToolCallingIntegrationTests`)
