# Plan: Implement Tool Calling for Azure Providers

## Context
The "Comprehensive Review" identified a missing integration test for Azure OpenAI Tool Calling. Upon further inspection, the implementation itself is missing for both `Cisharpai.Azure` providers: **Azure OpenAI** and **Azure AI Inference**.

This plan outlines the steps to implement the `IToolCallingFeature` for both providers and add the missing integration tests.

## 1. Domain Models Update

We need to update the internal request/response models to support tool definitions and tool call messages. These models are internal to the provider implementation but map to the public `Cisharpai.Models`.

### Azure OpenAI Models
File: `src/Cisharpai.Azure/AzureOpenAi/Models/AzureOpenAiChatRequest.cs`

- **Update `AzureOpenAiChatMessage`**:
  - Add `[JsonPropertyName("tool_calls")] public List<AzureOpenAiToolCall>? ToolCalls { get; set; }`
  - Add `[JsonPropertyName("tool_call_id")] public string? ToolCallId { get; set; }`
- **Update `AzureOpenAiChatRequest`**:
  - Add `[JsonPropertyName("tools")] public List<AzureOpenAiToolDefinition>? Tools { get; set; }`
  - Add `[JsonPropertyName("tool_choice")] public object? ToolChoice { get; set; }`
- **Update `AzureOpenAiChatResponse`**:
  - Add tool call collections on message payloads so provider responses can be mapped back to domain `ToolCall` objects.
- **Create new model classes** (if not reusing from a shared location, otherwise define them locally in `Models/`):
  - `AzureOpenAiToolDefinition`
  - `AzureOpenAiToolFunction`
  - `AzureOpenAiToolCall`
  - `AzureOpenAiToolCallFunction`
  - *Reference `OpenAiChatCompletionClient.cs` for the structure as the APIs are identical.*

### Azure AI Inference Models
File: `src/Cisharpai.Azure/AzureAiInference/Models/AzureAiInferenceChatRequest.cs`

- **Update `AzureAiInferenceChatMessage`**:
  - Add `tool_calls` and `tool_call_id` properties.
- **Update `AzureAiInferenceChatRequest`**:
  - Add `tools` and `tool_choice` properties.
- **Update `AzureAiInferenceChatResponse`**:
  - Add tool call collections on message payloads and map them back to domain `ToolCall` objects.
- **Create new model classes**:
  - Validates if we can share the tool definition classes with Azure OpenAI (e.g. move to a `Common` namespace within `Cisharpai.Azure`) or duplicate them if namespaces are strictly separated. Given the folder structure, they seem separated, so duplication is safer for isolation.

## 2. Client Implementation

We need to implement the `IToolCallingFeature` interface in both clients.

### Azure OpenAI Client
File: `src/Cisharpai.Azure/AzureOpenAi/AzureOpenAiChatCompletionClient.cs`

1.  **Implements Interface**: Add `IToolCallingFeature` to the class declaration.
2.  **Register Feature**: In the constructor, add `features.Set<IToolCallingFeature>(this);`.
3.  **Implement Method**: `GetChatCompletionWithToolsAsync`.
    - Map `ToolCallingOptions` to provider-specific `Tools` and `ToolChoice`.
    - Execute request.
    - Map response to `ToolCallingResponse`.
4.  **Update `MapMessages`**:
    - Handle `LlmRole.Tool` -> map to message with `tool_call_id`.
    - Handle `LlmRole.Assistant` with `ToolCalls` -> map to message with `tool_calls`.
    - Handle request messages with tool calls (round-trip).

### Azure AI Inference Client
File: `src/Cisharpai.Azure/AzureAiInference/AzureAiInferenceChatCompletionClient.cs`

1.  **Implements Interface**: Add `IToolCallingFeature` to the class declaration.
2.  **Register Feature**: In the constructor, add `features.Set<IToolCallingFeature>(this);`.
3.  **Implement Method**: `GetChatCompletionWithToolsAsync`.
    - Similar logic to Azure OpenAI but using Inference models.
    - *Note*: Ensure the Inference endpoint supports the standard "tools" payload (it generally does for newer models).

## 3. Integration Tests

Create new integration test files to verify the feature against live endpoints.

### Azure OpenAI Tests
File: `src/Cisharpai.Integration.Tests/AzureOpenAi/AzureOpenAiToolCallingIntegrationTests.cs`

- **Test Case 1**: Basic tool calling.
  - Define a tool (e.g., `get_weather`).
  - Send a user prompt ("What's the weather?").
  - Assert response contains a `ToolCall`.
- **Test Case 2**: Tool execution loop (optional but recommended).
  - Get tool call.
  - Respond with tool result.
  - Verify final answer.
- *Reference `AnthropicToolCallingIntegrationTests.cs` or `OpenAiToolCallingIntegrationTests.cs` for the test structure.*

### Azure AI Inference Tests
File: `src/Cisharpai.Integration.Tests/AzureAiInference/AzureAiInferenceToolCallingIntegrationTests.cs`

- Same test cases as above, checking that the generic Inference client handles tools correctly for both configured models (the environment variable provides two distinct models; run/skip per model if unsupported to avoid false negatives).

## 4. Execution Order

1.  **Models**: Update all internal JSON models first.
2.  **Azure OpenAI IMPL**: Implement `AzureOpenAiChatCompletionClient`.
3.  **Azure OpenAI TEST**: Create and run `AzureOpenAiToolCallingIntegrationTests`.
4.  **Azure AI Inference IMPL**: Implement `AzureAiInferenceChatCompletionClient`.
5.  **Azure AI Inference TEST**: Create and run `AzureAiInferenceToolCallingIntegrationTests`.
