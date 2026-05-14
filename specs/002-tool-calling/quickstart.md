# Quickstart: Tool Calling

## Prerequisites

Tool calling requires the base chat completion setup from [spec 001](../001-standard-chat-completion/quickstart.md). Ensure you have a provider configured.

## Step 1: Discover the Feature

```csharp
using Cisharpai.Features.Chat;

IChatCompletionClient client = /* any provider */;
var toolFeature = client.Features.Get<IToolCallingFeature>();
// toolFeature is non-null for all 5 providers
```

## Step 2: Define Tools

```csharp
using Cisharpai.Models;
using System.Text.Json;

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
    Tools: [new ToolDefinition("get_weather", "Get current weather for a city", parameters)]);
```

## Step 3: Send a Request

```csharp
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "What's the weather in Paris?")],
    Model: "gpt-4.1-nano");

var response = await toolFeature!.GetChatCompletionWithToolsAsync(request, toolOptions);

if (response.ToolCalls is not null)
{
    foreach (var toolCall in response.ToolCalls)
    {
        Console.WriteLine($"Tool: {toolCall.FunctionName}");
        Console.WriteLine($"Args: {toolCall.Arguments.GetRawText()}");
    }
}
```

## Step 4: Close the Loop (Multi-Turn)

Send tool results back to get the final response:

```csharp
var toolCall = response.ToolCalls![0];

// Execute your tool
var weatherResult = "{\"temperature\": \"22C\", \"condition\": \"sunny\"}";

// Build multi-turn messages
var messages = new List<LlmMessage>
{
    new(LlmRole.User, "What's the weather in Paris?"),
    new(LlmRole.Assistant, "", ToolCalls: [toolCall]),
    new(LlmRole.Tool, weatherResult, ToolCallId: toolCall.Id)
};

var followUp = new ChatCompletionRequest(Messages: messages, Model: "gpt-4.1-nano");
var finalResponse = await toolFeature.GetChatCompletionWithToolsAsync(followUp, toolOptions);

Console.WriteLine(finalResponse.Content);
// → "The weather in Paris is sunny, 22°C."
```

## Controlling Tool Selection

```csharp
// Force the model to call a specific tool
var options = new ToolCallingOptions(
    Tools: [weatherTool, calculatorTool],
    ToolChoice: ToolChoice.Specific("get_weather"));

// Model must call at least one tool
var options = new ToolCallingOptions(
    Tools: [weatherTool],
    ToolChoice: ToolChoice.Required);

// Prevent tool calls (model generates text only)
var options = new ToolCallingOptions(
    Tools: [weatherTool],
    ToolChoice: ToolChoice.None);

// Model decides (default)
var options = new ToolCallingOptions(
    Tools: [weatherTool],
    ToolChoice: ToolChoice.Auto);
```

## Error Handling

```csharp
var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

if (!response.IsSuccess)
{
    Console.WriteLine($"Error: {response.ErrorMessage}");
    return;
}
```

Validation errors (empty tools, invalid definitions) throw `ArgumentException` before the HTTP call.

## Switching Providers

The same code works across all five providers — only the DI registration changes:

```csharp
// Any of these gives you tool calling via the same IToolCallingFeature interface
services.AddOpenAiClient(o => { o.ApiKey = "sk-..."; });
services.AddAnthropicClient(o => { o.ApiKey = "sk-ant-..."; });
services.AddAzureOpenAiClient(o => { o.Endpoint = "https://..."; o.DeploymentName = "gpt-4o"; });
services.AddCohereChatClient(o => { o.ApiKey = "..."; });
```

## Unit Testing with Fakes

```csharp
using Cisharpai.Testing;

var fake = new FakeChatCompletionClient();
fake.EnqueueToolCallingResponse(
    FakeResponses.ToolCall("get_weather", "{\"city\": \"Paris\"}"));

var toolFeature = fake.Features.Get<IToolCallingFeature>()!;
var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

Assert.That(response.ToolCalls![0].FunctionName, Is.EqualTo("get_weather"));
Assert.That(fake.ReceivedToolCallingRequests, Has.Count.EqualTo(1));
```

To disable tool calling in the fake (e.g., test feature discovery):

```csharp
var fake = new FakeChatCompletionClient(FakeChatFeatures.Streaming | FakeChatFeatures.JsonOutput);
// FakeChatFeatures.ToolCalling not included → Features.Get<IToolCallingFeature>() returns null
```

## Verify It Works

1. Set your API key in environment variables or user secrets.
2. Run the console demo: `dotnet run --project src/Cisharp.Console`
3. Select "Tool Calling" from the Spectre.Console menu.
4. The demo defines a weather tool, sends a request, simulates tool execution, and shows the final response.
