# Feature Specification: Tool Calling

**Feature Branch**: `feature/gh-specify`

**Created**: 2026-05-14

**Status**: Complete (Retrospec)

**Input**: User description: "Now retrospect the feature that will support tool calling"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Invoke Tools via Chat Completion (Priority: P1)

As a developer, I want to define functions and let the model decide when to call them so that I can build applications where the LLM can interact with external systems (APIs, databases, calculators).

**Why this priority**: This is the core value of tool calling — without it, models cannot take actions in the real world.

**Independent Test**: Define a weather tool with JSON Schema parameters, send a user message asking about weather, verify the response contains a `ToolCall` with the correct function name and valid arguments matching the schema.

**Acceptance Scenarios**:

1. **Given** a tool definition with name "get_weather" and a JSON Schema for parameters, **When** I send a request asking about weather, **Then** the response contains at least one `ToolCall` with `FunctionName == "get_weather"` and `Arguments` matching the schema.
2. **Given** a request that doesn't require tool use, **When** sent with tool definitions, **Then** `ToolCalls` is null and `Content` contains the model's text response.
3. **Given** multiple tool definitions, **When** the model needs multiple tools, **Then** the response contains multiple `ToolCall` entries with distinct IDs.

---

### User Story 2 - Multi-Turn Tool Conversation (Priority: P1)

As a developer, I want to send tool results back to the model so that it can incorporate them into its final response, enabling a complete tool-calling loop.

**Why this priority**: Single-turn tool calling is rarely useful without the ability to close the loop with tool results.

**Independent Test**: Get tool calls from the model, construct follow-up messages with `LlmRole.Tool` messages containing results, send them back, and verify the model generates a natural-language response incorporating the tool output.

**Acceptance Scenarios**:

1. **Given** a tool call response, **When** I send back a Tool message with the tool result and matching `ToolCallId`, **Then** the model generates a text response incorporating the tool output.
2. **Given** multiple tool calls, **When** I send back results for all of them, **Then** the model synthesizes all results into a coherent response.
3. **Given** an Anthropic client, **When** I send tool results, **Then** the library correctly maps them as `user` role messages with `tool_result` content blocks (Anthropic's format).

---

### User Story 3 - Control Tool Selection Strategy (Priority: P2)

As a developer, I want to control whether the model must call a tool, can choose freely, or must call a specific tool so that I can enforce predictable behavior in my application.

**Why this priority**: Selection control is important for production applications but the default (Auto) works for most use cases.

**Independent Test**: Send the same request with different `ToolChoice` values and verify the model's behavior changes accordingly.

**Acceptance Scenarios**:

1. **Given** `ToolChoice.Auto`, **When** the model decides a tool isn't needed, **Then** it may return text without tool calls.
2. **Given** `ToolChoice.Required`, **When** sent with tool definitions, **Then** the model always returns at least one tool call.
3. **Given** `ToolChoice.None`, **When** sent with tool definitions, **Then** the model never returns tool calls.
4. **Given** `ToolChoice.Specific("get_weather")`, **When** sent with multiple tools, **Then** the model calls specifically "get_weather" (or degrades to Required for providers that don't support it, like Cohere).

---

### User Story 4 - Define Tool Parameters with JSON Schema (Priority: P1)

As a developer, I want to describe tool parameters using JSON Schema so that the model understands what arguments are valid and generates conforming calls.

**Why this priority**: Without parameter schemas, tool calls would have unpredictable argument shapes.

**Independent Test**: Define a tool with a JSON Schema specifying required and optional properties with types, send a request, verify the returned `Arguments` conform to the schema.

**Acceptance Scenarios**:

1. **Given** a tool with `Parameters` as a valid JSON Schema object, **When** the tool definition is validated, **Then** `Validate()` succeeds.
2. **Given** a tool with empty name, **When** `Validate()` is called, **Then** it throws `ArgumentException`.
3. **Given** a tool with `Parameters` that is not a JSON object, **When** `Validate()` is called, **Then** it throws `ArgumentException`.

---

### User Story 5 - Handle Tool Calling Errors Without Exceptions (Priority: P2)

As a developer, I want tool calling API errors returned as `IsSuccess=false` on the response so that I can handle failures through normal control flow consistent with the rest of the library.

**Why this priority**: Consistent error handling is a library design principle; important but secondary to basic functionality.

**Independent Test**: Send a tool calling request with invalid configuration or to an endpoint that fails, verify `response.IsSuccess == false` and `response.ErrorMessage` is populated.

**Acceptance Scenarios**:

1. **Given** a provider returns an API error for a tool calling request, **When** the response is mapped, **Then** `IsSuccess == false`, `ErrorMessage` is populated, and `ToolCalls` is null.
2. **Given** empty `Tools` list in `ToolCallingOptions`, **When** `Validate()` is called, **Then** it throws `ArgumentException` before making the HTTP request.

---

### User Story 6 - Stream Tool Call Deltas (Priority: P3)

As a developer, I want to receive partial tool call information during streaming so that I can start processing tool calls before the full response is complete.

**Why this priority**: Streaming tool calls is an advanced optimization; most applications wait for the full response.

**Independent Test**: Start a streaming request with tool definitions, collect chunks, verify at least one chunk has a non-null `ToolCallDelta` with function name and argument fragments.

**Acceptance Scenarios**:

1. **Given** a streaming request with tool definitions, **When** the model calls a tool, **Then** chunks include `ToolCallDelta` with incremental `ArgumentsDelta` strings.
2. **Given** a `ToolCallDelta`, **Then** it contains `Index`, optional `Id` (first chunk only), optional `FunctionName` (first chunk only), and `ArgumentsDelta`.

---

### User Story 7 - Test Tool Calling with Fakes (Priority: P2)

As a developer writing unit tests, I want to use the fake client to simulate tool calling responses so that I can test my tool-calling logic without real API calls.

**Why this priority**: Testing support is essential for production adoption.

**Independent Test**: Configure `FakeChatCompletionClient` with a queued `ToolCallingResponse`, call `GetChatCompletionWithToolsAsync`, verify the response matches and the request was captured.

**Acceptance Scenarios**:

1. **Given** a `FakeChatCompletionClient` with an enqueued `ToolCallingResponse`, **When** I call `GetChatCompletionWithToolsAsync`, **Then** the enqueued response is returned and the request is captured in `ReceivedToolCallingRequests`.
2. **Given** `FakeResponses.ToolCall("get_weather", "{\"city\": \"Paris\"}")`, **Then** it creates a valid `ToolCallingResponse` with one tool call.
3. **Given** `FakeChatFeatures.ToolCalling` is not set, **When** I call `Features.Get<IToolCallingFeature>()`, **Then** it returns null.

---

### Edge Cases

- What happens when the model returns unparseable JSON in tool call arguments? → `ToolCallingHelper.MapResponseToolCalls` wraps it as a raw JSON string.
- What happens when Cohere receives `ToolChoice.Specific`? → Degrades to `REQUIRED` (Cohere API limitation).
- What happens when Anthropic receives `ToolChoice.None`? → Omits the `tool_choice` parameter entirely.
- What happens when `ToolCallingOptions.Tools` is null? → `Validate()` throws `ArgumentException` before any HTTP call.
- What happens when a tool call is made with a reasoning model (o1/o3/o4)? → `MaxCompletionTokens` is used instead of `MaxTokens`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide an `IToolCallingFeature` interface discoverable via the Feature Collection pattern.
- **FR-002**: The feature MUST accept a `ChatCompletionRequest` plus `ToolCallingOptions` and return a `ToolCallingResponse`.
- **FR-003**: `ToolCallingResponse` MUST wrap the base `ChatCompletionResponse` and add an optional list of `ToolCall` objects.
- **FR-004**: Each `ToolCall` MUST contain a provider-assigned ID, function name, and parsed JSON arguments.
- **FR-005**: System MUST support defining tools via `ToolDefinition` with name, description, JSON Schema parameters, and a strict flag.
- **FR-006**: System MUST support four tool selection strategies: Auto, None, Required, and Specific (with graceful degradation where unsupported).
- **FR-007**: System MUST validate `ToolCallingOptions` and `ToolDefinition` before making API calls, throwing `ArgumentException` for invalid input.
- **FR-008**: System MUST implement tool calling for all 5 providers: OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere.
- **FR-009**: System MUST map provider-specific tool call formats transparently (OpenAI function calls, Anthropic tool_use blocks, Cohere tool_calls).
- **FR-010**: System MUST support multi-turn tool conversations via `LlmMessage` with `ToolCallId` (Tool role) and `ToolCalls` (Assistant role).
- **FR-011**: System MUST return API errors as `ToolCallingResponse.Error()` without throwing exceptions.
- **FR-012**: System MUST support streaming tool call deltas via `ToolCallDelta` in `ChatCompletionChunk`.
- **FR-013**: System MUST provide a `ToolResult` record for representing tool execution outputs.
- **FR-014**: System MUST provide `FakeResponses.ToolCall()` and `FakeResponses.ToolCalls()` factory methods for testing.
- **FR-015**: System MUST support `IncludeRawResponse` and `ExtraParameters` on tool calling requests (inherited from base request).
- **FR-016**: Cohere provider MUST set `strict_tools: true` when all tool definitions have `Strict == true`.

### Key Entities

- **ToolDefinition**: A function the model can invoke — name, description, JSON Schema parameters, strict flag.
- **ToolCall**: The model's request to invoke a function — provider-assigned ID, function name, parsed JSON arguments.
- **ToolChoice**: Selection strategy — Auto (model decides), None (no tools), Required (must call), Specific (named tool).
- **ToolCallingOptions**: Configuration for a tool calling request — list of tool definitions + choice strategy.
- **ToolCallingResponse**: Response wrapping ChatCompletionResponse + optional list of ToolCalls.
- **ToolResult**: The output of executing a tool — tool call ID, content, error flag.
- **ToolCallDelta**: Incremental tool call data during streaming — index, ID, function name, arguments fragment.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 5 providers pass unit tests verifying tool definition mapping, tool call response parsing, and tool choice mapping.
- **SC-002**: Multi-turn tool conversations (request → tool calls → tool results → final response) work end-to-end for all 5 providers in integration tests.
- **SC-003**: `ToolChoice` mapping is correct for each provider's native format (OpenAI string/object, Anthropic auto/any/tool, Cohere AUTO/REQUIRED).
- **SC-004**: Invalid tool definitions are rejected at validation time before any HTTP call.
- **SC-005**: Unparseable tool call arguments are gracefully handled (wrapped as raw string).
- **SC-006**: `FakeChatCompletionClient` supports queuing and capturing tool calling requests.
- **SC-007**: Streaming tool call deltas contain incremental argument data.

## Assumptions

- Tool calling builds on the base `IChatCompletionClient` infrastructure (spec 001).
- All providers support basic tool calling (at minimum: define tools, get tool calls, send results back).
- JSON Schema is the universal parameter definition format across all providers.
- Provider-specific limitations (e.g., Cohere's lack of Specific tool choice) are handled via graceful degradation, not errors.
- Anthropic's `tool_use`/`tool_result` content block format is fundamentally different from the OpenAI-style function calling format, requiring custom mapping.
- Tool call argument JSON parsing may fail for some models — the library handles this gracefully rather than throwing.

## Retrospec Metadata

**Generated**: 2026-05-14
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 35+ files across 8 projects
**Reference implementation branch**: feature/gh-specify
