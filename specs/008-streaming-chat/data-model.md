# Data Model: Streaming Chat Completions

## Unified Models (Core Package)

### ChatCompletionChunk

**File**: `src/Cisharpai/Models/ChatCompletionChunk.cs`
**Type**: Immutable C# record (positional)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Content` | `string` | Yes | Content delta text. May be empty for non-content events (usage, finish). |
| `FinishReason` | `string?` | No | Set on the final chunk. Values are provider-specific: `"stop"` (OpenAI/Azure), `"end_turn"` (Anthropic), `"COMPLETE"` (Cohere), `"completed"` (Responses API). |
| `Model` | `string?` | No | Model identifier. Present on every chunk for OpenAI/Azure (from SSE), derived from `message_start` for Anthropic, set from request model for Cohere. |
| `PromptTokens` | `int?` | No | Input token count. Only populated on the final chunk when provider includes usage data. |
| `CompletionTokens` | `int?` | No | Output token count. Only populated on the final chunk when provider includes usage data. |
| `ToolCallDelta` | `ToolCallDelta?` | No | Incremental tool call information. Only populated for OpenAI/Azure providers during tool-calling streams. |

### ToolCallDelta

**File**: `src/Cisharpai/Models/ChatCompletionChunk.cs` (same file)
**Type**: Immutable C# record (positional)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Index` | `int` | Yes | Zero-based index of the tool call in the response's tool_calls array. |
| `Id` | `string?` | No | Tool call ID. Typically present only on the first delta for each tool call. |
| `FunctionName` | `string?` | No | Function name. Typically present only on the first delta for each tool call. |
| `ArgumentsDelta` | `string?` | No | Incremental JSON string fragment of the function arguments. |

## Provider-Specific SSE Event Models

### OpenAI Legacy (Chat Completions API)

**File**: `src/Cisharpai.OpenAi/Models/OpenAiStreamChunk.cs`

| Class | Purpose |
|-------|---------|
| `OpenAiStreamChunk` | Top-level SSE data payload. Fields: `Id`, `Model`, `Choices`, `Usage`. |
| `OpenAiStreamChoice` | Per-choice wrapper. Fields: `Delta`, `FinishReason`, `Index`. |
| `OpenAiStreamDelta` | Content delta. Fields: `Content`, `Role`, `ToolCalls`. |
| `OpenAiStreamToolCallDelta` | Tool call delta within a choice. Fields: `Index`, `Id`, `Function`. |
| `OpenAiStreamToolCallFunction` | Function delta. Fields: `Name`, `Arguments`. |

**Stream termination**: `data: [DONE]` sentinel line.

### OpenAI Responses API (GPT-5)

**File**: `src/Cisharpai.OpenAi/Models/OpenAiResponsesStreamEvent.cs`

| Class | Purpose |
|-------|---------|
| `OpenAiResponsesStreamEvent` | Event with `Type`, `Delta` (text), `Response` (completed response). |

**Event types**: `response.output_text.delta` (text chunks), `response.completed` (final response with model/usage).
**Stream termination**: `data: [DONE]` sentinel line.

### Azure OpenAI (Chat Completions API)

**File**: `src/Cisharpai.Azure/AzureOpenAi/Models/AzureOpenAiStreamChunk.cs`

Identical structure to OpenAI legacy: `AzureOpenAiStreamChunk`, `AzureOpenAiStreamChoice`,
`AzureOpenAiStreamDelta`, `AzureOpenAiStreamToolCallDelta`, `AzureOpenAiStreamToolCallFunction`.

**Stream termination**: `data: [DONE]` sentinel line.

### Azure OpenAI (Responses API / GPT-5)

**File**: `src/Cisharpai.Azure/AzureOpenAi/Models/AzureOpenAiResponsesStreamEvent.cs`

Same structure as OpenAI Responses API: `AzureOpenAiResponsesStreamEvent` with `Type`, `Delta`, `Response`.

### Azure AI Inference

**File**: `src/Cisharpai.Azure/AzureAiInference/Models/AzureAiInferenceStreamChunk.cs`

Identical structure to OpenAI legacy: `AzureAiInferenceStreamChunk`, `AzureAiInferenceStreamChoice`,
`AzureAiInferenceStreamDelta`, `AzureAiInferenceStreamToolCallDelta`, `AzureAiInferenceStreamToolCallFunction`.

**Stream termination**: `data: [DONE]` sentinel line.

### Anthropic

**File**: `src/Cisharpai.Anthropic/Models/AnthropicStreamEvent.cs`

| Class | Purpose |
|-------|---------|
| `AnthropicStreamEvent` | Event with `Type`, `Index`, `Delta`, `Usage`, `Message`. |
| `AnthropicStreamDelta` | Delta object with `Type`, `Text`, `StopReason`. |
| `AnthropicStreamMessage` | Message metadata from `message_start`: `Model`, `Usage`. |

**Event types**: `message_start` (model + input tokens), `content_block_delta` (text via `text_delta`),
`message_delta` (stop reason + output tokens). Ignored: `ping`, `content_block_start`, `content_block_stop`, `message_stop`.
**Stream termination**: No `[DONE]` sentinel. Stream ends when the server closes the connection.

### Cohere

**File**: `src/Cisharpai.Cohere/Models/CohereStreamEvent.cs`

| Class | Purpose |
|-------|---------|
| `CohereStreamEvent` | Event with `Type`, `Delta`. |
| `CohereStreamDelta` | Delta with `Message`, `FinishReason`, `Usage`. |
| `CohereStreamMessage` | Contains `Content`. |
| `CohereStreamContent` | Contains `Text`. |
| `CohereStreamUsage` | Contains `BilledUnits`. |
| `CohereStreamBilledUnits` | Contains `InputTokens`, `OutputTokens`. |

**Event types**: `stream-start` (no useful metadata), `content-delta` (text via nested message.content.text),
`message-end` (finish reason + billed_units usage).
**Stream termination**: No `[DONE]` sentinel. Stream ends when the server closes the connection.

## Relationships

```
IStreamingChatFeature
    └── returns IAsyncEnumerable<ChatCompletionChunk>
                                    ├── Content (string)
                                    ├── FinishReason (string?)
                                    ├── Model (string?)
                                    ├── PromptTokens (int?)
                                    ├── CompletionTokens (int?)
                                    └── ToolCallDelta?
                                            ├── Index (int)
                                            ├── Id (string?)
                                            ├── FunctionName (string?)
                                            └── ArgumentsDelta (string?)

LlmHttpClient.PostStreamAsync<TRequest>
    └── returns IAsyncEnumerable<string>  (raw JSON lines)
            └── consumed by each provider's GetChatCompletionStreamAsync
                    └── deserialized into provider-specific models
                            └── mapped to unified ChatCompletionChunk
```
