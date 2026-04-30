# Tool Calling (Function Calling)

Tool calling allows you to define functions that the model can invoke during a conversation. The model decides when and how to call tools, and you provide the results back to continue the conversation.

## Quick Start

```csharp
using Cisharpai;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using System.Text.Json;

// 1. Get the tool calling feature
IChatCompletionClient client = /* any provider */;
var toolFeature = client.Features.Get<IToolCallingFeature>();

// 2. Define tools
var parameters = JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "city": { "type": "string", "description": "The city name" }
        },
        "required": ["city"]
    }
    """).RootElement.Clone();

var toolOptions = new ToolCallingOptions(
    Tools: [new ToolDefinition("get_weather", "Get current weather", parameters)]);

// 3. Send a request
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "What's the weather in Paris?")],
    Model: "gpt-4.1-nano");

var response = await toolFeature!.GetChatCompletionWithToolsAsync(request, toolOptions);

// 4. Check for tool calls
if (response.ToolCalls is not null)
{
    foreach (var toolCall in response.ToolCalls)
    {
        Console.WriteLine($"Tool: {toolCall.FunctionName}");
        Console.WriteLine($"Args: {toolCall.Arguments.GetRawText()}");
    }
}
```

## Multi-Turn Conversation

After receiving tool calls, send the results back to get the final response:

```csharp
// Step 1: Get tool calls from model
var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);
var toolCall = response.ToolCalls![0];

// Step 2: Execute the tool (your code)
var result = ExecuteWeatherTool(toolCall.Arguments);

// Step 3: Send results back
var messages = new List<LlmMessage>
{
    new(LlmRole.User, "What's the weather in Paris?"),
    new(LlmRole.Assistant, "", ToolCalls: [toolCall]),
    new(LlmRole.Tool, result, ToolCallId: toolCall.Id)
};

var followUp = new ChatCompletionRequest(Messages: messages, Model: "gpt-4.1-nano");
var finalResponse = await toolFeature.GetChatCompletionWithToolsAsync(followUp, toolOptions);

Console.WriteLine(finalResponse.Content); // "The weather in Paris is sunny, 22C."
```

## ToolChoice Options

Control how the model selects tools:

| ToolChoice | Behavior |
|------------|----------|
| `null` (default) | Provider default (usually auto) |
| `ToolChoice.Auto` | Model decides whether to call a tool |
| `ToolChoice.None` | Model must not call any tool |
| `ToolChoice.Required` | Model must call at least one tool |
| `ToolChoice.Specific("name")` | Model must call the named tool |

```csharp
// Force the model to use a specific tool
var options = new ToolCallingOptions(
    Tools: [weatherTool],
    ToolChoice: ToolChoice.Specific("get_weather"));

// Prevent tool calls
var options = new ToolCallingOptions(
    Tools: [weatherTool],
    ToolChoice: ToolChoice.None);
```

## Core Models

| Type | Description |
|------|-------------|
| `ToolDefinition` | Defines a tool (Name, Description, Parameters as JSON Schema, Strict flag) |
| `ToolCall` | Model's request to invoke a tool (Id, FunctionName, Arguments) |
| `ToolChoice` | Controls tool selection strategy (Auto, None, Required, Specific) |
| `ToolCallingOptions` | Configuration passed to the feature (Tools list, ToolChoice) |
| `ToolCallingResponse` | Response wrapping ChatCompletionResponse with ToolCalls |

## Strict Mode

By default, `ToolDefinition.Strict` is `true`, requesting the provider to enforce the parameter schema strictly. Set it to `false` if you want flexible parameter handling:

```csharp
var tool = new ToolDefinition("get_weather", "Get weather", parameters, Strict: false);
```

Cohere uses this to set `strict_tools: true` when all tools are strict. Other providers handle strictness through their own mechanisms.

## Provider Support

| Feature | OpenAI | Azure OpenAI | Azure AI Inference | Anthropic | Cohere |
|---------|--------|--------------|-------------------|-----------|--------|
| Basic Tool Calling | Yes | Yes | Yes | Yes | Yes |
| ToolChoice.Auto | Yes | Yes | Yes | Yes (auto) | Yes (AUTO) |
| ToolChoice.None | Yes | Yes | Yes | Omitted | Yes (NONE) |
| ToolChoice.Required | Yes | Yes | Yes | Yes (any) | Yes (REQUIRED) |
| ToolChoice.Specific | Yes | Yes | Yes | Yes (tool) | Degrades to REQUIRED |
| Parallel Tool Calls | Yes | Yes | Yes | Yes | Yes |
| Strict Mode | Via schema | Via schema | Via schema | Via input_schema | Via strict_tools flag |

## Provider Differences

### OpenAI

- Standard function calling format with `tools` array and `tool_choice` parameter.
- Supports all `ToolChoice` variants including `Specific` (mapped to `{type: "function", function: {name: "..."}}"`).
- Tool call arguments are JSON-encoded strings that are parsed to `JsonElement`.

### Azure OpenAI

- Identical JSON format to OpenAI -- uses `tools` array and `tool_choice` parameter.
- Deployment-based routing: endpoint is `openai/deployments/{deployment}/chat/completions`.
- Reasoning model detection (o1/o3/o4/gpt-5) automatically uses `max_completion_tokens`.
- All `ToolChoice` variants supported including `Specific`.

### Azure AI Inference

- Uses OpenAI-compatible chat completions format at `models/chat/completions` endpoint.
- Model ID is included in the request body (not the URL).
- Support depends on the deployed model -- some catalog models may not support tool calling.
- Reasoning model detection (o1/o3/o4/gpt-5) automatically uses `max_completion_tokens`.
- All `ToolChoice` variants supported including `Specific`.

### Anthropic

- Uses `tool_use` content blocks in assistant responses and `tool_result` content blocks for results.
- Tool results are sent as `user` role messages with `tool_result` content blocks (not a `tool` role).
- `ToolChoice.Required` maps to `{type: "any"}`.
- `ToolChoice.Specific` maps to `{type: "tool", name: "..."}`.
- `ToolChoice.None` is handled by omitting the `tool_choice` parameter.
- Tool call arguments are already JSON objects (no string parsing needed).

### Cohere

- Uses Cohere v2 chat API format with `tools` array.
- `ToolChoice` values are mapped to uppercase strings: `AUTO`, `NONE`, `REQUIRED`.
- `ToolChoice.Specific` is not natively supported; degrades to `REQUIRED`.
- When all tools have `Strict: true`, sets `strict_tools: true` on the request.
- Uses snake_case serialization for all properties.

## Error Handling

Tool calling follows the library's standard error handling pattern -- no exceptions for API errors:

```csharp
var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

if (!response.IsSuccess)
{
    Console.WriteLine($"Error: {response.ErrorMessage}");
    return;
}
```

See [Provider Features](provider-features.md) for the full feature matrix.
