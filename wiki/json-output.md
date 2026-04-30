# JSON Output (Structured Outputs & JSON Mode)

## Overview

Cisharpai supports two forms of JSON output enforcement via the `IJsonOutputFeature`:

- **JSON Mode** (`json_object`): Forces the model to produce valid JSON output without schema enforcement. Works on all model families. Requires the word "JSON" in the system message for OpenAI-compatible models.
- **Structured Outputs** (`json_schema`): Forces the model to produce output conforming to a caller-supplied JSON Schema with strict enforcement. Only supported on gpt-4o-2024-08-06+, gpt-4.1, o-series, and gpt-5 class models.

## Quick Start

### JSON Mode

```csharp
var client = provider.GetRequiredService<IChatCompletionClient>();
var jsonFeature = client.Features.Get<IJsonOutputFeature>();

if (jsonFeature is not null)
{
    var request = new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "List 3 colors as JSON")],
        Model: "gpt-4o-mini",
        Temperature: 0);

    var options = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

    var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);
    // response.Content contains valid JSON
}
```

### Structured Outputs

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
```

## Provider Support Matrix

| Provider | JSON Mode | Structured Outputs | Notes |
|----------|-----------|-------------------|-------|
| OpenAI (Legacy: gpt-4o, gpt-4.1) | Yes | Yes | Via `response_format` in Chat Completions API |
| OpenAI (Reasoning: o1, o3, o4) | Yes | Yes | Via `response_format` in Chat Completions API |
| OpenAI (GPT-5) | Yes | Yes | Via `text.format` in Responses API |
| Azure OpenAI | Yes | Yes | Requires api-version 2024-08-01-preview+ for json_schema |
| Azure AI Inference | Yes | Varies | Depends on deployed model (Phi, Llama, Mistral) |
| Anthropic | Yes | Yes | Via system message injection (JSON Mode) and `output_config.format` (Structured Outputs) |
| Cohere | Yes | Yes | Via `response_format` type `json_object` with optional `json_schema` |

## JSON Schema Guidelines

When using Structured Outputs (`JsonOutputMode.JsonSchema`):

1. **Root must be an object**: The top-level `type` must be `"object"`.
2. **All fields required**: Every property must be listed in `"required"`.
3. **No additional properties**: Set `"additionalProperties": false` at every object level.
4. **Supported types**: `string`, `number`, `integer`, `boolean`, `array`, `object`, `null`.
5. **Nesting limits**: Up to 5 levels of nesting for most models.
6. **Total properties**: Max ~100 properties across the entire schema.

## Refusal Handling

When using Structured Outputs, the model may refuse to generate output for safety reasons. Check the `Refusal` property:

```csharp
var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

if (response.Refusal is not null)
{
    Console.WriteLine($"Model refused: {response.Refusal}");
}
else
{
    var json = response.Content; // Valid JSON matching the schema
}
```

## Feature Discovery

Check if a client supports JSON output via the Feature Collection pattern:

```csharp
IChatCompletionClient client = /* any provider */;

if (client.Features.Get<IJsonOutputFeature>() is { } jsonFeature)
{
    // JSON output is supported
}
else
{
    // Fallback: use regular chat completion and parse manually
}
```

Currently, `IJsonOutputFeature` is registered on:
- `OpenAiChatCompletionClient`
- `AzureOpenAiChatCompletionClient`
- `AzureAiInferenceChatCompletionClient`
- `AnthropicChatCompletionClient`
- `CohereChatCompletionClient`

## ExtraParameters Escape Hatch

For provider-specific options not yet exposed by the library, use `ExtraParameters`:

```csharp
var extra = JsonDocument.Parse("""{"response_format":{"type":"json_object"}}""").RootElement;
var request = new ChatCompletionRequest(
    Messages: [...],
    Model: "gpt-4o",
    ExtraParameters: extra);
```

`ExtraParameters` is deep-merged into the generated provider request after typed options are serialized, so it can also override typed values. For example, Azure OpenAI exposes `AzureOpenAiClientOptions.ReasoningEffort`, but a single request can still override it:

```csharp
var extra = JsonDocument.Parse("""{"reasoning_effort":"high"}""").RootElement;
var request = new ChatCompletionRequest(
    Messages: [...],
    Model: "gpt-5-nano",
    MaxTokens: 200,
    ExtraParameters: extra);
```

## Model Compatibility

### Structured Outputs (json_schema)
- gpt-4o-2024-08-06 and later
- gpt-4.1, gpt-4.1-mini, gpt-4.1-nano
- o1, o3, o3-mini, o4-mini
- gpt-5, gpt-5-mini, gpt-5-nano

### JSON Mode (json_object)
- All GPT-4 and GPT-3.5 models
- All reasoning models
- All GPT-5 models
- Azure AI Inference models (varies by model)

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "must contain the word 'JSON'" error | The library auto-injects "JSON" into the system message for JSON Mode, but if using ExtraParameters directly you must add it manually |
| Schema validation errors | Ensure `additionalProperties: false` at every object level and all properties are in `required` |
| Unsupported model error | Check the model compatibility table above; not all models support json_schema |
| Refusal instead of content | The model refused for safety reasons; check `response.Refusal` and adjust your prompt |
