# Implementation Plan: Unified Tool Calling

## Goal
Implement a provider-agnostic tool calling feature that supports OpenAI, Anthropic, and Cohere. The implementation will resolve differences in wire formats (JSON structure, naming, conventions) while exposing a unified C# object model to the consumer. This allows switching between providers without changing tool definition or execution logic.

## Architecture

### 1. Core Models (`Cisharpai.Models`)
We will introduce a set of shared models that know nothing about specific API formats.

*   **Location**: `src/Cisharpai/Models/Tools/`
*   **Types**:
     *   `ToolDefinition`: Represents a function tool with Name, Description, and Parameters.
     *   `JsonSchemaDefinition`: A rigid C# representation of JSON Schema (used for parameters).
     *   `ToolCall`: Represents an invocation request from the model (`Id`, `FunctionName`, `Arguments` as `JsonElement`).
    *   `ToolResult`: Represents the output of a tool execution (`ToolCallId`, `Content`, `IsError`, optional `ContentBlocks`).
     *   `ToolChoice`: Controls tool selection strategy (`Auto`, `None`, `Required`, `Specific`).

### 2. Integration with Chat Completion
*   **Breaking change accepted**: Extend the public record constructors in `ChatCompletionRequest` and `ChatCompletionResponse` to carry tool metadata directly.
*   **Update `ChatCompletionRequest`**: Add properties for `Tools` (List of `ToolDefinition`) and `ToolChoice`.
*   **Update `ChatCompletionResponse`**: Add property for `ToolCalls` (List of `ToolCall`).
*   **Update `LlmMessage`**: Represent tool results inside the existing message pipeline by extending the message model to carry tool result metadata and content blocks.
    *   Add `ToolCallId` and `ContentBlocks` (for Anthropic tool_result blocks).
    *   Add a `Tool` role value to `LlmRole`.

### 3. Conversion Layer
*   **Location**: `src/Cisharpai/Features/Tools/Conversion/` (or strictly internal to providers if preferred, but a shared factory approach was proposed).
*   **Concept**: `IToolCallConverter` handles the translation between the unified model and provider-specific JSON.
    *   Serialize `ToolDefinition` list and `ToolChoice`.
    *   Deserialize `ToolCalls` from provider response.
    *   Serialize `ToolResult` list back to provider format.

### 4. Execution Layer (Optional helper)
*   **Location**: `src/Cisharpai/Features/Tools/Execution/`
*   **Concept**: A `ToolRegistry` to map definitions to C# delegates, handling dispatch, argument deserialization, and error capturing.

## Provider Analysis & Strategy

### OpenAI
*   **Format**: `{ type: "function", function: { ... } }`.
*   **Args**: JSON-encoded string.
*   **Results**: `role: "tool"`, `tool_call_id`.
*   **Special**: `strict: true` for strict schema adherence using the same JSON Schema structure.

### Anthropic
*   **Format**: Top-level object `{ name, description, input_schema }`.
*   **Args**: Parsed JSON object.
*   **Results**: `user` message with `{ type: "tool_result", ... }` content blocks.
*   **Special**: `is_error` flag, rich content (images in results).

### Cohere (V2)
*   **Format**: Similar to OpenAI `{ type: "function", function: { ... } }`.
*   **Args**: JSON-encoded string.
*   **Results**: `role: "tool"`.
*   **Special**: Tool choice uses uppercase (`AUTO`, `REQUIRED`, `NONE`) and does not support specific tool selection.

## Tasks

### Phase 1: Core Definitions
1.  [ ] Create `src/Cisharpai/Models/Tools/` directory.
2.  [ ] Implement `JsonSchemaDefinition` and `JsonSchemaBuilder`. defining the supported subset of JSON Schema.
3.  [ ] Implement `ToolDefinition` and `ToolDefinitionBuilder`.
4.  [ ] Implement `ToolCall`, `ToolResult` (include `ContentBlocks`), and `ToolChoice`.
5.  [ ] Update `ChatCompletionRequest` in `Cisharpai.Models` to include `IReadOnlyList<ToolDefinition>? Tools` and `ToolChoice? ToolChoice` (breaking change accepted).
6.  [ ] Update `ChatCompletionResponse` to expose `IReadOnlyList<ToolCall>? ToolCalls` (breaking change accepted).
7.  [ ] Extend `LlmRole` with `Tool` and extend `LlmMessage` with `ToolCallId` and `ContentBlocks` (tool_result payload).

### Phase 2: Conversion Infrastructure
8.  [ ] Create `src/Cisharpai/Features/Tools/` directory.
9.  [ ] Define `IToolCallConverter` interface.
    *   `JsonElement SerializeToolDefinitions(IEnumerable<ToolDefinition> tools)`
    *   `JsonElement SerializeToolChoice(ToolChoice toolChoice)`
    *   `IEnumerable<ToolCall> DeserializeToolCalls(JsonElement response)`
    *   `JsonElement SerializeToolResults(IEnumerable<ToolResult> results)`

### Phase 3: OpenAI Implementation (`Cisharpai.OpenAi`)
10. [ ] Implement `OpenAiToolCallConverter` (handles string-encoded args, "function" wrapper).
11. [ ] Update `OpenAiChatCompletionClient` to map the Request's `Tools` and `ToolChoice` using the converter.
12. [ ] Handle the response mapping to populate `ToolCalls` in `ChatCompletionResponse`.

### Phase 4: Anthropic Implementation (`Cisharpai.Anthropic`)
13. [ ] Implement `AnthropicToolCallConverter` (handles `input_schema` key, object args, tool_use content blocks).
14. [ ] Update `AnthropicChatCompletionClient` to use the converter.
15. [ ] Ensure `ToolResult` serialization produces the correct `user` message structure with `tool_result` content blocks.

### Phase 5: Cohere Implementation (`Cisharpai.Cohere`)
16. [ ] Implement `CohereToolCallConverter` (handles uppercase choices, degrades specific tool choice to required, assumes V2 API).
17. [ ] Update `CohereChatCompletionClient`.

### Phase 6: Execution Utilities & Testing
18. [ ] Implement `ToolRegistry` for easy dispatching of tool calls to local C# functions.
19. [ ] Add Unit/Integration tests verifying the roundtrip of tools and results for each provider.

## Findings from Analysis

| Aspect | OpenAI | Anthropic | Cohere V2 | Strategy |
|---|---|---|---|---|
| **Schema Key** | `parameters` | `input_schema` | `parameters` | Model has `Parameters`, converter maps key. |
| **Arguments** | JSON String | JSON Object | JSON String | Unified model exposes `JsonElement`. Converter parses string if needed. |
| **Result Role** | `tool` | `user` | `tool` | Provider client handles message construction. |
| **Error Flag** | ❌ | ✅ | ❌ | Model has `IsError`. OpenAI/Cohere converter prepends `[ERROR]`. |
| **Strict Mode** | Per-tool | ❌ | Request-level | Model has `Strict`. OpenAI uses it. Cohere implementation checks if all tools are strict to enable request flag. |

## Notes & Risks

*   **Cohere Specific Tool Choice**: Cohere does not support forcing a named tool. The converter will degrade `ToolChoice.ForFunction("name")` to `ToolChoice.Required` (force any tool) and log a warning if possible.
*   **Anthropic Strict Mode**: Anthropic has no strict mode flag. The `Strict` property on tools will be ignored by the Anthropic converter.
*   **Rich Content**: Anthropic supports images in tool results. The unified `ToolResult` model supports `ContentBlocks` to allow this, but OpenAI/Cohere converters will only use the text representation.
*   **Round-Tripping**: Because arguments are parsed from strings for some providers, strict byte-for-byte round-tripping of arguments isn't guaranteed (whitespace/ordering), but semantic equivalence is maintained via `JsonElement`.
