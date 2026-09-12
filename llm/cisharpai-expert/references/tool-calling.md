# Tool Calling

## Quick Start

```csharp
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using System.Text.Json;

var toolFeature = client.Features.Get<IToolCallingFeature>();
if (toolFeature is null) throw new NotSupportedException("Provider doesn't support tools");

// 1. Define tools
var tools = new[]
{
    new ToolDefinition(
        Name: "get_weather",
        Description: "Get current weather for a location",
        Parameters: JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "location": { "type": "string", "description": "City name" },
                "unit": { "type": "string", "enum": ["celsius", "fahrenheit"] }
            },
            "required": ["location"],
            "additionalProperties": false
        }
        """).RootElement,
        Strict: true)
};

// 2. Send request with tools
var response = await toolFeature.GetChatCompletionWithToolsAsync(
    request,
    new ToolCallingOptions(Tools: tools));

// 3. Handle tool calls
if (response.ToolCalls is { Count: > 0 })
{
    foreach (var call in response.ToolCalls)
    {
        var result = ExecuteTool(call.FunctionName, call.Arguments);
        // Add tool result to conversation and continue
    }
}
```

## Core Models

**ToolDefinition:**
- `Name` — Function name
- `Description` — What the tool does
- `Parameters` — JSON Schema (JsonElement)
- `Strict` — Enforce schema strictly (default: `true`)

**ToolCall:**
- `Id` — Unique identifier for this call
- `FunctionName` — Which tool to invoke
- `Arguments` — Parsed arguments (JsonElement)

**ToolChoice Options:**
- `null` — Provider default
- `ToolChoice.Auto` — Model decides
- `ToolChoice.None` — No tool calls
- `ToolChoice.Required` — Must call at least one tool
- `ToolChoice.Specific("name")` — Force specific tool

## Multi-Turn Tool Calling

```csharp
var messages = new List<LlmMessage>
{
    new(LlmRole.User, "What's the weather in Paris?")
};

while (true)
{
    var request = new ChatCompletionRequest(Messages: messages);
    var response = await toolFeature.GetChatCompletionWithToolsAsync(
        request,
        new ToolCallingOptions(Tools: tools));

    if (response.ToolCalls is not { Count: > 0 })
    {
        Console.WriteLine(response.Content);
        break;
    }

    // Add assistant message with tool calls
    messages.Add(new LlmMessage(
        LlmRole.Assistant,
        response.Content,
        ToolCalls: response.ToolCalls));

    // Execute each tool and add results
    foreach (var call in response.ToolCalls)
    {
        var result = ExecuteTool(call.FunctionName, call.Arguments);
        messages.Add(new LlmMessage(
            LlmRole.Tool,
            result,
            ToolCallId: call.Id));
    }
}
```

## Provider Differences

| Aspect | OpenAI/Azure | Anthropic | Cohere |
|--------|-------------|-----------|--------|
| Format | `tools` array | `tool_use` blocks | `tools` array |
| Results | `tool` role message | `tool_result` in user msg | `tool` role message |
| Strict mode | Supported | Supported | Supported |
| Specific choice | Supported | Supported | Degrades to REQUIRED |
| Case | camelCase | camelCase | snake_case |

All differences are handled internally by Cisharpai — use the same code for all providers.
