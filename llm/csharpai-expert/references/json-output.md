# JSON Output & Structured Outputs

## Two Modes

| Mode | Description | Provider Support |
|------|-------------|-----------------|
| `JsonOutputMode.JsonObject` | Valid JSON, no schema enforcement | All 5 providers |
| `JsonOutputMode.JsonSchema` | Strict schema enforcement | All 5 providers |

## Quick Start

```csharp
var jsonFeature = client.Features.Get<IJsonOutputFeature>();

// JSON Mode (no schema)
var response = await jsonFeature.GetJsonChatCompletionAsync(request,
    new JsonOutputOptions { Mode = JsonOutputMode.JsonObject });

// Structured Outputs (with schema)
var response = await jsonFeature.GetJsonChatCompletionAsync(request,
    new JsonOutputOptions
    {
        Mode = JsonOutputMode.JsonSchema,
        SchemaName = "weather_response",
        Schema = JsonDocument.Parse("""
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
        """).RootElement
    });
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
var request = new ChatCompletionRequest
{
    Messages =
    [
        new("system", "Respond in JSON format."),
        new("user", "List 3 colors with hex codes")
    ]
};
```

## ExtraParameters for Unsupported Options

```csharp
var options = new JsonOutputOptions
{
    Mode = JsonOutputMode.JsonSchema,
    Schema = schema,
    ExtraParameters = JsonDocument.Parse("""
    { "strict": true }
    """).RootElement
};
```
