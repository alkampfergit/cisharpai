# Contract: IJsonOutputFeature

**Source**: `src/Cisharpai/Features/Chat/IJsonOutputFeature.cs`

## Interface Definition

```csharp
public interface IJsonOutputFeature
{
    Task<ChatCompletionResponse> GetChatCompletionWithJsonOutputAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOutputOptions,
        CancellationToken cancellationToken = default);
}
```

## Discovery

```csharp
var jsonFeature = client.Features.Get<IJsonOutputFeature>();
// Non-null for all 5 providers
```

## Behavioral Contract

### Input

| Parameter | Type | Description |
|-----------|------|-------------|
| request | `ChatCompletionRequest` | Standard chat completion request |
| jsonOutputOptions | `JsonOutputOptions` | Mode (JsonMode/JsonSchema), schema details, strict flag |
| cancellationToken | `CancellationToken` | Cooperative cancellation |

**Preconditions**:
- `jsonOutputOptions` is validated before the HTTP call.
- For JsonMode: the library auto-injects "JSON" into the system message if not already present.
- For JsonSchema: `SchemaName` and `JsonSchema` must be non-null, and `JsonSchema` must be valid JSON.

### Output

Returns `ChatCompletionResponse` (the standard base response):

| Scenario | IsSuccess | Content | Refusal |
|----------|-----------|---------|---------|
| JSON generated | `true` | Valid JSON string | `null` |
| Schema refusal | `true` | May be empty | Non-null refusal text |
| API error | `false` | Empty | `null`; `ErrorMessage` populated |

### Error Handling

- **Validation failures**: `ArgumentException` thrown before HTTP call.
- **API errors**: Returned as `ChatCompletionResponse.Error(...)`.
- **Safety refusals**: Surfaced via `response.Refusal` (not an error).

## Provider Implementations

| Provider | Class | JsonMode Wire | JsonSchema Wire |
|----------|-------|---------------|-----------------|
| OpenAI (legacy) | `OpenAiChatCompletionClient` | `response_format: {type: "json_object"}` | `response_format: {type: "json_schema", json_schema: {name, strict, schema}}` |
| OpenAI (GPT-5) | `OpenAiChatCompletionClient` | `text.format: {type: "json_object"}` | `text.format: {type: "json_schema", name, strict, schema}` |
| Azure OpenAI | `AzureOpenAiChatCompletionClient` | Same as OpenAI legacy | Same as OpenAI legacy |
| Azure AI Inference | `AzureAiInferenceChatCompletionClient` | Same as OpenAI legacy | Same as OpenAI legacy |
| Anthropic | `AnthropicChatCompletionClient` | System message injection + code fence stripping | `output_config: {format: {type: "json_schema", schema: ...}}` |
| Cohere | `CohereChatCompletionClient` | `response_format: {type: "json_object"}` | `response_format: {type: "json_object", json_schema: ...}` |

## Usage Examples

### JSON Mode

```csharp
var options = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);
var json = JsonDocument.Parse(response.Content);
```

### Structured Outputs

```csharp
var options = new JsonOutputOptions(
    Mode: JsonOutputMode.JsonSchema,
    SchemaName: "color_list",
    JsonSchema: """{"type":"object","properties":{"colors":{"type":"array"}},"required":["colors"],"additionalProperties":false}""");

var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

if (response.Refusal is not null)
    Console.WriteLine($"Refused: {response.Refusal}");
else
    Console.WriteLine(response.Content); // Conforms to schema
```

### Unit Testing

```csharp
var fake = new FakeChatCompletionClient();
fake.EnqueueJsonOutputResponse(FakeResponses.Chat("{\"colors\":[]}"));

var jsonFeature = fake.Features.Get<IJsonOutputFeature>()!;
var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

Assert.That(response.Content, Is.EqualTo("{\"colors\":[]}"));
Assert.That(fake.ReceivedJsonOutputRequests, Has.Count.EqualTo(1));
```
