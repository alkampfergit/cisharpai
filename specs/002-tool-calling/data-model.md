# Data Model: Tool Calling

## Entities

### ToolDefinition

Defines a tool (function) the model can invoke.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Name | `string` | Required, non-whitespace | Validated by `Validate()` — throws `ArgumentException` if empty |
| Description | `string` | Required | Human-readable description for the model |
| Parameters | `JsonElement` | Must be `JsonValueKind.Object` | JSON Schema describing function parameters; validated by `Validate()` |
| Strict | `bool` | Default: `true` | Enables strict schema enforcement (OpenAI Structured Outputs); Cohere maps to `strict_tools` |

**Source**: `src/Cisharpai/Models/ToolDefinition.cs`

---

### ToolCall

The model's request to invoke a specific tool.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | `string` | Provider-assigned | Used to correlate with `ToolResult.ToolCallId` |
| FunctionName | `string` | Matches a `ToolDefinition.Name` | Name of the function to invoke |
| Arguments | `JsonElement` | Parsed JSON | If parsing fails, wrapped as a raw JSON string |

**Source**: `src/Cisharpai/Models/ToolCall.cs`

---

### ToolChoice

Controls how the model selects which tool to call. Abstract record with four concrete subtypes.

| Variant | Static Factory | Behavior |
|---------|----------------|----------|
| AutoChoice | `ToolChoice.Auto` | Model decides whether to call a tool or generate text |
| NoneChoice | `ToolChoice.None` | Model must not call any tool |
| RequiredChoice | `ToolChoice.Required` | Model must call at least one tool |
| SpecificChoice | `ToolChoice.Specific(name)` | Model must call the named function |

**Properties**:
| Property | Type | Notes |
|----------|------|-------|
| IsSpecific | `bool` (virtual) | `true` only for `SpecificChoice` |
| FunctionName | `string?` (virtual) | Non-null only for `SpecificChoice` |

**Provider mapping**:
| Variant | OpenAI/Azure | Anthropic | Cohere |
|---------|-------------|-----------|--------|
| Auto | `"auto"` | `{type: "auto"}` | `"AUTO"` |
| None | `"none"` | Omitted entirely | `"NONE"` |
| Required | `"required"` | `{type: "any"}` | `"REQUIRED"` |
| Specific | `{type: "function", function: {name: "..."}}` | `{type: "tool", name: "..."}` | Degrades to `"REQUIRED"` |

**Source**: `src/Cisharpai/Models/ToolChoice.cs`

---

### ToolCallingOptions

Configuration for a tool-calling request.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Tools | `IReadOnlyList<ToolDefinition>` | Required, non-empty | Validated by `Validate()` — throws if null/empty; also validates each tool |
| ToolChoice | `ToolChoice?` | Optional, defaults to null (provider default) | Controls selection strategy |

**Source**: `src/Cisharpai/Models/ToolCallingOptions.cs`

---

### ToolCallingResponse

Response from a tool-calling chat completion.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| ChatCompletion | `ChatCompletionResponse` | Required | Underlying response with content, tokens, raw data |
| ToolCalls | `IReadOnlyList<ToolCall>?` | Null if model chose text | List of tool invocations requested by the model |

**Convenience delegates**:
| Property | Delegates to |
|----------|-------------|
| IsSuccess | `ChatCompletion.IsSuccess` |
| ErrorMessage | `ChatCompletion.ErrorMessage` |
| Content | `ChatCompletion.Content` |

**Static factory**: `ToolCallingResponse.Error(errorMessage, rawResponseJson?)` — creates an error response.

**Source**: `src/Cisharpai/Models/ToolCallingResponse.cs`

---

### ToolResult

The output of executing a tool, sent back to the model.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| ToolCallId | `string` | Must match `ToolCall.Id` | Correlates result to the original tool call |
| Content | `string` | Required | Textual content returned by the tool |
| IsError | `bool` | Default: `false` | Indicates tool execution failed |

**Source**: `src/Cisharpai/Models/ToolResult.cs`

---

### ToolCallDelta

Incremental tool call data emitted during streaming.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Index | `int` | Zero-based | Position in the tool calls array |
| Id | `string?` | First chunk only | Provider-assigned tool call ID |
| FunctionName | `string?` | First chunk only | Name of the function being called |
| ArgumentsDelta | `string?` | Incremental | Fragment of the JSON arguments string |

**Source**: `src/Cisharpai/Models/ChatCompletionChunk.cs`

---

## Provider Wire DTOs

These are internal serialization types, not part of the public API.

### OpenAI / Azure OpenAI / Azure AI Inference

| Wire Type | Maps To | Notes |
|-----------|---------|-------|
| `OpenAiToolDefinition` | `ToolDefinition` | `{type: "function", function: {name, description, parameters, strict}}` |
| `OpenAiToolCall` | `ToolCall` | `{id, type: "function", function: {name, arguments}}` |
| `OpenAiToolChoiceObject` | `ToolChoice.Specific` | `{type: "function", function: {name}}` |

Azure OpenAI and Azure AI Inference use identical wire format classes (`AzureOpenAiToolDefinition`, `AzureAiInferenceToolDefinition`).

### Anthropic

| Wire Type | Maps To | Notes |
|-----------|---------|-------|
| `AnthropicToolDefinition` | `ToolDefinition` | `{name, description, input_schema}` — uses `input_schema` instead of `parameters` |
| `AnthropicToolChoice` | `ToolChoice` | `{type, name?}` — type values: `auto`, `any`, `tool` |
| Content block `tool_use` | `ToolCall` | Arguments are already JSON objects (no string parsing) |
| Content block `tool_result` | `ToolResult` | Sent as `user` role message, not `tool` role |

### Cohere

| Wire Type | Maps To | Notes |
|-----------|---------|-------|
| `CohereToolDefinition` | `ToolDefinition` | `{type: "function", function: {name, description, parameters}}` |
| `CohereToolCall` | `ToolCall` | `{id, type: "function", function: {name, arguments}}` |
| `tool_choice` string | `ToolChoice` | Uppercase strings: `AUTO`, `NONE`, `REQUIRED` |
| `strict_tools` flag | `ToolDefinition.Strict` | Set to `true` when all tools have `Strict == true` |

---

## Entity Relationships

```
ChatCompletionRequest ──messages──► LlmMessage
       │                               │
       │                          ToolCallId? (Tool role)
       │                          ToolCalls?  (Assistant role)
       │
       ▼
IToolCallingFeature
       │
       ├── ToolCallingOptions
       │       ├── Tools: List<ToolDefinition>
       │       └── ToolChoice?
       │
       └── ToolCallingResponse
               ├── ChatCompletionResponse
               └── ToolCalls: List<ToolCall>?
                       │
                       ▼
                   ToolResult (sent back via LlmMessage)

ChatCompletionChunk ──► ToolCallDelta? (streaming)
```

## Validation Rules

| Entity | Rule | Error |
|--------|------|-------|
| ToolDefinition | Name must be non-empty | `ArgumentException("Tool name is required.")` |
| ToolDefinition | Parameters must be `JsonValueKind.Object` | `ArgumentException("Parameters must be a JSON object...")` |
| ToolCallingOptions | Tools must be non-null and non-empty | `ArgumentException("At least one tool definition is required.")` |
| ToolCallingOptions | Each tool in Tools passes `ToolDefinition.Validate()` | Cascaded from ToolDefinition |
