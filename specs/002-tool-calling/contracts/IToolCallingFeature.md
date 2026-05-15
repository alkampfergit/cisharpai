# Contract: IToolCallingFeature

**Source**: `src/Cisharpai/Features/Chat/IToolCallingFeature.cs`

## Interface Definition

```csharp
public interface IToolCallingFeature
{
    Task<ToolCallingResponse> GetChatCompletionWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken = default);
}
```

## Discovery

The feature is discoverable via the Feature Collection pattern:

```csharp
var toolFeature = client.Features.Get<IToolCallingFeature>();
if (toolFeature is not null)
{
    // Provider supports tool calling
}
```

All five providers register `IToolCallingFeature` in their feature collection.

## Behavioral Contract

### Input

| Parameter | Type | Description |
|-----------|------|-------------|
| request | `ChatCompletionRequest` | Standard chat completion request (messages, model, temperature, etc.) |
| toolOptions | `ToolCallingOptions` | Tool definitions + optional selection strategy |
| cancellationToken | `CancellationToken` | Cooperative cancellation |

**Preconditions**:
- `toolOptions` is validated before the HTTP call: Tools must be non-null, non-empty, and each ToolDefinition must pass `Validate()`.
- `request.Messages` follows standard chat completion rules from spec 001.
- For multi-turn tool conversations, `request.Messages` includes:
  - An `Assistant` role message with `ToolCalls` (the model's tool invocation)
  - A `Tool` role message with `ToolCallId` matching the tool call ID (the tool's result)

### Output

Returns `ToolCallingResponse` containing:

| Scenario | IsSuccess | ToolCalls | Content |
|----------|-----------|-----------|---------|
| Model calls tools | `true` | Non-null list of `ToolCall` | May be empty |
| Model generates text | `true` | `null` | Non-empty text |
| API error | `false` | `null` | Empty; `ErrorMessage` populated |

### Error Handling

- **Validation failures** (null/empty tools, invalid tool definitions): `ArgumentException` thrown before HTTP call.
- **API errors**: Caught as `LlmHttpRequestException`, returned as `ToolCallingResponse.Error(errorMessage, rawResponseJson)`.
- **Unparseable tool arguments**: Wrapped as a raw JSON string value (no exception).
- **Network/config errors**: Propagated as exceptions (consistent with spec 001 pattern).

### Threading

- Thread-safe: all implementations are stateless and reentrant.
- Cancellation is cooperative via `CancellationToken`.

## Provider Implementations

| Provider | Class | Wire Format |
|----------|-------|-------------|
| OpenAI | `OpenAiChatCompletionClient` | `tools` array with `{type: "function", function: {...}}` |
| Azure OpenAI | `AzureOpenAiChatCompletionClient` | Same as OpenAI |
| Azure AI Inference | `AzureAiInferenceChatCompletionClient` | Same as OpenAI |
| Anthropic | `AnthropicChatCompletionClient` | `tools` array with `{name, description, input_schema}`; results as `user` role `tool_result` blocks |
| Cohere | `CohereChatCompletionClient` | `tools` array with `{type: "function", function: {...}}`; uppercase choice strings |

## Shared Helper

`ToolCallingHelper` (in `src/Cisharpai/Helpers/ToolCallingHelper.cs`) provides three generic mapping methods used by all providers:

| Method | Purpose |
|--------|---------|
| `MapResponseToolCalls<T>` | Converts provider-specific tool call lists to `List<ToolCall>` with safe JSON argument parsing |
| `MapToolChoice` | Maps `ToolChoice?` to provider-agnostic string/object (`"auto"`, `"none"`, `"required"`, specific object) |
| `MapStreamToolCallDelta<T>` | Converts provider-specific streaming deltas to `ToolCallDelta` |

## Usage Examples

### Single-Turn Tool Call

```csharp
var toolOptions = new ToolCallingOptions(
    Tools: [new ToolDefinition("get_weather", "Get current weather", parametersSchema)]);

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "What's the weather in Paris?")]);

var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

if (response.ToolCalls is not null)
{
    foreach (var tc in response.ToolCalls)
        Console.WriteLine($"{tc.FunctionName}({tc.Arguments.GetRawText()})");
}
```

### Multi-Turn Tool Conversation

```csharp
// 1. Get tool calls
var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);
var toolCall = response.ToolCalls![0];

// 2. Execute tool and send result back
var messages = new List<LlmMessage>
{
    new(LlmRole.User, "What's the weather in Paris?"),
    new(LlmRole.Assistant, "", ToolCalls: [toolCall]),
    new(LlmRole.Tool, weatherResult, ToolCallId: toolCall.Id)
};

var followUp = new ChatCompletionRequest(Messages: messages);
var finalResponse = await toolFeature.GetChatCompletionWithToolsAsync(followUp, toolOptions);
Console.WriteLine(finalResponse.Content);
```

### Controlling Tool Selection

```csharp
// Force a specific tool
var options = new ToolCallingOptions(
    Tools: [weatherTool, calculatorTool],
    ToolChoice: ToolChoice.Specific("get_weather"));

// Prevent tool calling
var options = new ToolCallingOptions(
    Tools: [weatherTool],
    ToolChoice: ToolChoice.None);
```

### Unit Testing with Fakes

```csharp
var fake = new FakeChatCompletionClient();
fake.EnqueueToolCallingResponse(FakeResponses.ToolCall("get_weather", "{\"city\": \"Paris\"}"));

var toolFeature = fake.Features.Get<IToolCallingFeature>()!;
var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

Assert.That(response.ToolCalls![0].FunctionName, Is.EqualTo("get_weather"));
Assert.That(fake.ReceivedToolCallingRequests, Has.Count.EqualTo(1));
```
