# Data Model: Standard Chat Completion

## Core Entities

### ChatCompletionRequest

**Type**: `sealed record` (immutable)
**File**: `src/Cisharpai/Models/ChatCompletionRequest.cs`

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| Messages | `IReadOnlyList<LlmMessage>` | Yes | Must contain at least one message |
| Model | `string?` | No | Falls back to provider `DefaultModel`; if both null, throws `InvalidOperationException` |
| Temperature | `double?` | No | Provider-specific range (typically 0.0–2.0) |
| MaxTokens | `int?` | No | Anthropic defaults to 8192 if null; others use provider default |
| IncludeRawResponse | `bool` | No | Default `false`. When true, response includes raw JSON |
| ExtraParameters | `JsonElement?` | No | Must be a JSON object if provided; deep-merged into outbound request |
| ReasoningEffort | `string?` | No | For reasoning models: "low", "medium", "high" |

### ChatCompletionResponse

**Type**: `sealed record` (immutable)
**File**: `src/Cisharpai/Models/ChatCompletionResponse.cs`

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| Content | `string` | Yes | Empty string on error or refusal |
| Model | `string` | Yes | The actual model used (may differ from requested) |
| PromptTokens | `int` | Yes | >= 0 |
| CompletionTokens | `int` | Yes | >= 0 |
| RawResponseJson | `string?` | No | Non-null only when `IncludeRawResponse=true` |
| RawRequestJson | `string?` | No | Non-null only when `IncludeRawResponse=true` |
| Status | `string?` | No | Provider-specific status (e.g., "stop", "end_turn", "completed") |
| IncompleteReason | `string?` | No | Non-null when response was truncated |
| IsSuccess | `bool` | Yes | Default `true`. `false` for API errors |
| ErrorMessage | `string?` | No | Non-null when `IsSuccess=false` |
| Refusal | `string?` | No | Non-null when model refuses output for safety reasons |

**Static Factory**: `ChatCompletionResponse.Error(errorMessage, rawResponseJson?)` creates a failure response.

### LlmMessage

**Type**: `sealed record` (immutable)
**File**: `src/Cisharpai/Models/LlmMessage.cs`

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| Role | `LlmRole` | Yes | System, User, Assistant, or Tool |
| Content | `string` | Yes | Text content; empty when ContentParts is used |
| ToolCallId | `string?` | No | Required for Tool role messages |
| ToolCalls | `IReadOnlyList<ToolCall>?` | No | Present on Assistant messages with tool invocations |
| ContentParts | `IReadOnlyList<MessageContentPart>?` | No | Multimodal content; overrides Content when present |

**Static Factories**:
- `LlmMessage.WithImage(text, imagePath)` — user message with local image file
- `LlmMessage.WithBase64Image(text, base64Data, mediaType)` — user message with base64 image

### LlmRole

**Type**: `enum`
**File**: `src/Cisharpai/Models/LlmRole.cs`
**Values**: `System`, `User`, `Assistant`, `Tool`

### MessageContentPart (Discriminated Union)

**Type**: `abstract record` with sealed subtypes
**File**: `src/Cisharpai/Models/MessageContentPart.cs`

| Subtype | Fields | Description |
|---------|--------|-------------|
| `TextContentPart` | `Text: string` | Text content in a multimodal message |
| `ImageFileContentPart` | `FilePath: string` | Local image file, auto-converted to base64 by provider |
| `ImageBase64ContentPart` | `Base64Data: string`, `MediaType: string` | Raw base64 image with MIME type |

## Infrastructure Entities

### IFeatureCollection

**Type**: `interface` (extends `IEnumerable<KeyValuePair<Type, object>>`)
**File**: `src/Cisharpai/Features/IFeatureCollection.cs`

| Method | Returns | Description |
|--------|---------|-------------|
| `Get<T>()` | `T?` | Returns feature instance or null |
| `Set<T>(T instance)` | `void` | Registers a feature |

**Implementation**: `FeatureCollection` — backed by `ConcurrentDictionary<Type, object>`.

### LlmHttpClient

**Type**: `sealed class`
**File**: `src/Cisharpai/LlmHttpClient.cs`

Not a model/DTO but the shared HTTP transport. Key methods:
- `PostAsync<TRequest, TResponse>` — standard request/response
- `PostWithRawAsync<TRequest, TResponse>` — returns raw JSON alongside deserialized response
- `PostStreamAsync<TRequest>` — yields raw SSE `data:` payloads as `IAsyncEnumerable<string>`

### LlmHttpRequestException

**Type**: `sealed class` (extends `HttpRequestException`)
**File**: `src/Cisharpai/LlmHttpRequestException.cs`

| Field | Type | Description |
|-------|------|-------------|
| `StatusCode` | `HttpStatusCode` | HTTP status code |
| `ResponseBody` | `string?` | Raw error response body |

## Provider Options Entities

### OpenAiClientOptions

**File**: `src/Cisharpai.OpenAi/OpenAiClientOptions.cs`

| Property | Type | Default |
|----------|------|---------|
| BaseUrl | `string` | `https://api.openai.com/v1/` |
| ApiKey | `string` | `""` |
| Organization | `string?` | null |
| ReasoningEffort | `string?` | null |
| TextVerbosity | `string?` | null |
| DefaultModel | `string?` | null |

### AnthropicClientOptions

**File**: `src/Cisharpai.Anthropic/AnthropicClientOptions.cs`

| Property | Type | Default |
|----------|------|---------|
| BaseUrl | `string` | `https://api.anthropic.com/v1/` |
| ApiKey | `string` | `""` |
| ApiVersion | `string` | `"2023-06-01"` |
| DefaultModel | `string?` | null |

### AzureOpenAiClientOptions

**File**: `src/Cisharpai.Azure/AzureOpenAi/AzureOpenAiClientOptions.cs`

| Property | Type | Default |
|----------|------|---------|
| Endpoint | `string` | `""` (from base) |
| ApiKey | `string` | `""` (from base) |
| ApiVersion | `string` | `"2024-10-21"` |
| DeploymentName | `string` | `""` |
| DefaultModel | `string?` | null |
| ModelName | `string?` | null (explicit model for opaque deployments) |
| ReasoningEffort | `string?` | null |
| TextVerbosity | `string?` | null |

### AzureAiInferenceClientOptions

**File**: `src/Cisharpai.Azure/AzureAiInference/AzureAiInferenceClientOptions.cs`

| Property | Type | Default |
|----------|------|---------|
| Endpoint | `string` | `""` (from base) |
| ApiKey | `string` | `""` (from base) |
| ApiVersion | `string` | `"2024-05-01-preview"` |
| ModelId | `string` | `""` |

### CohereClientOptions

**File**: `src/Cisharpai.Cohere/CohereClientOptions.cs`

| Property | Type | Default |
|----------|------|---------|
| BaseUrl | `string` | `https://api.cohere.com/v2/` |
| ApiKey | `string` | `""` |
| DefaultModel | `string?` | null |

## Entity Relationships

```
ChatCompletionRequest
  └── Messages: LlmMessage[]
       ├── Role: LlmRole
       ├── Content: string
       ├── ContentParts: MessageContentPart[]?
       │    ├── TextContentPart
       │    ├── ImageFileContentPart
       │    └── ImageBase64ContentPart
       ├── ToolCallId: string? (for Tool role)
       └── ToolCalls: ToolCall[]? (for Assistant role)

IChatCompletionClient
  ├── GetChatCompletionAsync(request) → ChatCompletionResponse
  └── Features: IFeatureCollection
       ├── IStreamingChatFeature?
       ├── IJsonOutputFeature?
       ├── IToolCallingFeature?
       └── IGroundedChatFeature?

Provider Client (e.g. OpenAiChatCompletionClient)
  ├── implements IChatCompletionClient
  ├── implements feature interfaces
  ├── uses LlmHttpClient (HTTP transport)
  └── configured via ProviderClientOptions
```
