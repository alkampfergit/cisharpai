# JSON Output & Structured Outputs

## Two Modes

| Mode | Description | Provider Support |
|------|-------------|-----------------|
| `JsonOutputMode.JsonObject` | Valid JSON, no schema enforcement | All 5 providers |
| `JsonOutputMode.JsonSchema` | Strict schema enforcement | All 5 providers |

## Quick Start

```csharp
using Cisharpai.Features.Chat;
using Cisharpai.Models;

var jsonFeature = client.Features.Get<IJsonOutputFeature>();

// JSON Mode (no schema)
var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(
    request,
    new JsonOutputOptions(Mode: JsonOutputMode.JsonObject));

// Structured Outputs (with schema)
var schema = """
{
    "type": "object",
    "properties": {
        "temperature": { "type": "number" },
        "condition": { "type": "string" },
        "humidity": { "type": "integer" }
    },
    "required": ["temperature", "condition", "humidity"],
    "additionalProperties": false
}
""";

var structured = await jsonFeature.GetChatCompletionWithJsonOutputAsync(
    request,
    new JsonOutputOptions(
        Mode: JsonOutputMode.JsonSchema,
        SchemaName: "weather_response",
        JsonSchema: schema));
```

## Schema Requirements (Structured Outputs)

- Root must be `object` type
- All fields must be in `required` array
- Must include `"additionalProperties": false`
- Supported types: `string`, `number`, `integer`, `boolean`, `array`, `object`, `null`
- Max 5 nesting levels
- Max ~100 total properties

## Refusal Handling

With Structured Outputs, the model may refuse unsafe requests:

```csharp
if (response.Refusal is not null)
{
    Console.WriteLine($"Refused: {response.Refusal}");
    return;
}
var data = JsonSerializer.Deserialize<MyType>(response.Content);
```

## JSON Mode Notes

For OpenAI-compatible providers, include "JSON" in the system message:

```csharp
var request = new ChatCompletionRequest(
    Messages:
    [
        new LlmMessage(LlmRole.System, "Respond in JSON format."),
        new LlmMessage(LlmRole.User, "List 3 colors with hex codes")
    ]);
```

## Strict Structured Output

```csharp
var options = new JsonOutputOptions(
    Mode: JsonOutputMode.JsonSchema,
    SchemaName: "schema_name",
    JsonSchema: schema,
    Strict: true);
```
