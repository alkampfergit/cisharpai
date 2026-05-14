# Quickstart: Multimodal Images & JSON Output

## Prerequisites

Both features require the base chat completion setup from [spec 001](../001-standard-chat-completion/quickstart.md). Ensure you have a provider configured.

---

## Part A: Vision (Image Input)

### Send an Image from a File

```csharp
using Cisharpai;
using Cisharpai.Models;

IChatCompletionClient client = /* any provider */;

var message = LlmMessage.WithImage("Describe this image", "/path/to/photo.jpg");
var request = new ChatCompletionRequest(
    Messages: [message],
    Model: "gpt-4o");

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.Content);
```

### Send an Image from Base64

```csharp
var imageBytes = await File.ReadAllBytesAsync("/path/to/chart.png");
var base64 = Convert.ToBase64String(imageBytes);

var message = LlmMessage.WithBase64Image("What data does this chart show?", base64, "image/png");
var request = new ChatCompletionRequest(Messages: [message], Model: "gpt-4o");

var response = await client.GetChatCompletionAsync(request);
```

### Multiple Images in One Message

```csharp
var message = new LlmMessage(
    LlmRole.User,
    string.Empty,
    ContentParts:
    [
        new TextContentPart("Compare these two images:"),
        new ImageFileContentPart("/photos/before.jpg"),
        new ImageFileContentPart("/photos/after.jpg")
    ]);

var request = new ChatCompletionRequest(Messages: [message], Model: "gpt-4o");
var response = await client.GetChatCompletionAsync(request);
```

### Provider Compatibility

| Provider | Images Supported? |
|----------|-------------------|
| OpenAI | Yes |
| Azure OpenAI | Yes |
| Azure AI Inference | Yes (model-dependent) |
| Anthropic | Yes |
| Cohere | No (text extracted, images silently skipped) |

---

## Part B: JSON Output

### Discover the Feature

```csharp
using Cisharpai.Features.Chat;

var jsonFeature = client.Features.Get<IJsonOutputFeature>();
// Non-null for all 5 providers
```

### JSON Mode (Valid JSON, No Schema)

```csharp
using Cisharpai.Models;

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "List 3 colors as JSON")],
    Model: "gpt-4o-mini",
    Temperature: 0);

var options = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

var response = await jsonFeature!.GetChatCompletionWithJsonOutputAsync(request, options);
Console.WriteLine(response.Content); // Valid JSON
```

The library auto-injects "Respond in JSON." into the system message if needed.

### Structured Outputs (JSON Schema Enforcement)

```csharp
const string schema = """
    {
        "type": "object",
        "properties": {
            "colors": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "name": { "type": "string" },
                        "hex": { "type": "string" }
                    },
                    "required": ["name", "hex"],
                    "additionalProperties": false
                }
            }
        },
        "required": ["colors"],
        "additionalProperties": false
    }
    """;

var options = new JsonOutputOptions(
    Mode: JsonOutputMode.JsonSchema,
    SchemaName: "color_list",
    SchemaDescription: "A list of colors with hex codes",
    JsonSchema: schema);

var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

if (response.Refusal is not null)
    Console.WriteLine($"Model refused: {response.Refusal}");
else
    Console.WriteLine(response.Content); // Conforms to schema
```

### Error Handling

```csharp
// API errors
if (!response.IsSuccess)
{
    Console.WriteLine($"Error: {response.ErrorMessage}");
    return;
}

// Safety refusals (Structured Outputs only)
if (response.Refusal is not null)
{
    Console.WriteLine($"Refused: {response.Refusal}");
    return;
}
```

### Switching Providers

Same code works across all five providers:

```csharp
services.AddOpenAiClient(o => { o.ApiKey = "sk-..."; });
// or
services.AddAnthropicClient(o => { o.ApiKey = "sk-ant-..."; });
// or
services.AddAzureOpenAiClient(o => { o.Endpoint = "https://..."; o.DeploymentName = "gpt-4o"; });
```

### Unit Testing with Fakes

```csharp
using Cisharpai.Testing;

var fake = new FakeChatCompletionClient();
fake.EnqueueJsonOutputResponse(FakeResponses.Chat("{\"colors\":[{\"name\":\"red\",\"hex\":\"#ff0000\"}]}"));

var jsonFeature = fake.Features.Get<IJsonOutputFeature>()!;
var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

Assert.That(response.Content, Does.Contain("red"));
Assert.That(fake.ReceivedJsonOutputRequests, Has.Count.EqualTo(1));
```

## Verify It Works

1. Set your API key in environment variables or user secrets.
2. Run the console demo: `dotnet run --project src/Cisharp.Console`
3. Select "OpenAI JSON output" from the Spectre.Console menu.
4. The demo shows both JSON Mode and Structured Outputs with a color list schema.
