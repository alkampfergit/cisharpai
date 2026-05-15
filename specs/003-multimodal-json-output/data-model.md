# Data Model: Multimodal Images & JSON Output

## Vision / Multimodal Entities

### MessageContentPart (abstract base)

Discriminated-union base type for content parts in a multimodal message.

| Subtype | Fields | Description |
|---------|--------|-------------|
| `TextContentPart` | `Text: string` | Text segment within a multimodal message |
| `ImageFileContentPart` | `FilePath: string` | Image loaded from a local file path; converted to data URI at request time |
| `ImageBase64ContentPart` | `Base64Data: string`, `MediaType: string` | Image provided as raw base64 data with explicit MIME type |

All subtypes are sealed records inheriting from `MessageContentPart`.

**Source**: `src/Cisharpai/Models/MessageContentPart.cs`

---

### LlmMessage (extended for vision)

The existing `LlmMessage` record is extended with a `ContentParts` property for multimodal messages.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Role | `LlmRole` | Required | User, System, Assistant, Tool |
| Content | `string` | Required | Text content (ignored when ContentParts is non-null) |
| ContentParts | `IReadOnlyList<MessageContentPart>?` | Optional | When non-null, providers use this instead of Content |
| ToolCallId | `string?` | Optional | For Tool role messages |
| ToolCalls | `IReadOnlyList<ToolCall>?` | Optional | For Assistant role messages |

**Factory methods**:
| Method | Creates |
|--------|---------|
| `WithImage(text, filePath)` | User message with TextContentPart + ImageFileContentPart |
| `WithBase64Image(text, base64Data, mediaType)` | User message with TextContentPart + ImageBase64ContentPart |

**Source**: `src/Cisharpai/Models/LlmMessage.cs`

---

### ImageDataUriHelper

Static utility for image file handling.

| Method | Signature | Description |
|--------|-----------|-------------|
| `ToDataUriAsync` | `(string imagePath, CancellationToken) → Task<string>` | Reads file, encodes as `data:{mime};base64,{data}` |
| `GetMimeType` | `(string path) → string` | Detects MIME type from file extension |

**Supported MIME types**:
| Extension | MIME Type |
|-----------|----------|
| `.jpg`, `.jpeg` | `image/jpeg` |
| `.png` | `image/png` |
| `.webp` | `image/webp` |
| `.gif` | `image/gif` |
| Other | `application/octet-stream` |

**Source**: `src/Cisharpai/ImageDataUriHelper.cs`

---

## JSON Output Entities

### JsonOutputMode

Enum controlling the JSON output enforcement mode.

| Value | Wire Mapping | Description |
|-------|-------------|-------------|
| `JsonMode` | `response_format.type = "json_object"` | Forces valid JSON output without schema enforcement |
| `JsonSchema` | `response_format.type = "json_schema"` | Forces output conforming to a caller-supplied JSON Schema |

**Source**: `src/Cisharpai/Models/JsonOutputMode.cs`

---

### JsonOutputOptions

Configuration for JSON output behavior.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Mode | `JsonOutputMode` | Required | JsonMode or JsonSchema |
| SchemaName | `string?` | Required when Mode == JsonSchema | Identifier for the schema (used by OpenAI for caching) |
| SchemaDescription | `string?` | Optional | Human-readable description |
| JsonSchema | `string?` | Required when Mode == JsonSchema | JSON Schema string; must be valid JSON |
| Strict | `bool` | Default: `true` | Controls strict schema enforcement |

**Source**: `src/Cisharpai/Models/JsonOutputOptions.cs`

---

### JsonOutputHelper

Static utility for JSON output handling.

| Method | Signature | Description |
|--------|-----------|-------------|
| `EnsureJsonKeywordInSystemMessage` | `(messages, options, suffix?) → IReadOnlyList<LlmMessage>` | Appends "Respond in JSON." to system message if missing for JsonMode |
| `StripMarkdownCodeFences` | `(string content) → string` | Removes ` ```json ... ``` ` wrapping from content |

**Source**: `src/Cisharpai/Helpers/JsonOutputHelper.cs`

---

### ContentPartHelper

Static utility for content part mapping.

| Method | Signature | Description |
|--------|-----------|-------------|
| `ExtractStringContent` | `(object? content) → string` | Extracts text from raw string, JsonElement, or structured array/object |
| `MapOpenAiStyleContentPartsAsync<T>` | `(contentParts, createTextPart, createImageUrlPart, ct) → Task<List<T>>` | Maps content parts to OpenAI-style wire DTOs using data URIs |

**Source**: `src/Cisharpai/Helpers/ContentPartHelper.cs`

---

## Provider Wire DTOs

### OpenAI / Azure OpenAI / Azure AI Inference

**Response format**:
| Wire Type | Maps To | Notes |
|-----------|---------|-------|
| `OpenAiResponseFormat` | `JsonOutputOptions` | `{type: "json_object"}` or `{type: "json_schema", json_schema: {...}}` |
| `OpenAiJsonSchemaSpec` | Schema details | `{name, description?, strict, schema}` |
| `OpenAiTextFormat` | Responses API format | Flattened: `{type, name?, description?, strict?, schema?}` |

Azure variants (`AzureOpenAiResponseFormat`, `AzureAiInferenceResponseFormat`) are structurally identical.

**Content parts**:
| Wire Type | Maps To | Notes |
|-----------|---------|-------|
| `OpenAiContentPart` | `MessageContentPart` | `{type: "text", text}` or `{type: "image_url", image_url: {url}}` |
| `OpenAiImageUrl` | Image data | `{url: "data:{mime};base64,{data}"}` |

Azure variants (`AzureOpenAiContentPart`, `AzureAiInferenceContentPart`) are structurally identical.

### Anthropic

**Response format**:
| Wire Type | Maps To | Notes |
|-----------|---------|-------|
| `AnthropicOutputConfig` | `JsonOutputOptions` (JsonSchema) | `{format: {type: "json_schema", schema: ...}}` |

For JsonMode, Anthropic uses system message injection (no native json_object mode).

**Image content**:
| Wire Type | Maps To | Notes |
|-----------|---------|-------|
| `AnthropicImageSource` | `ImageBase64ContentPart` | `{type: "base64", media_type, data}` — raw base64, NOT data URI |

### Cohere

**Response format**:
| Wire Type | Maps To | Notes |
|-----------|---------|-------|
| `CohereChatResponseFormat` | `JsonOutputOptions` | `{type: "json_object", json_schema?}` |

For images: Cohere extracts text-only content; image parts are silently skipped.

---

## Entity Relationships

```
LlmMessage
├── Content: string (text-only path)
├── ContentParts: List<MessageContentPart>? (multimodal path)
│       ├── TextContentPart
│       ├── ImageFileContentPart ──► ImageDataUriHelper.ToDataUriAsync()
│       └── ImageBase64ContentPart
│
└── (used by both GetChatCompletionAsync and GetChatCompletionWithJsonOutputAsync)

IJsonOutputFeature
├── JsonOutputOptions
│       ├── JsonOutputMode (JsonMode | JsonSchema)
│       ├── SchemaName, JsonSchema, Strict
│       └── Validate()
├── JsonOutputHelper.EnsureJsonKeywordInSystemMessage()
├── JsonOutputHelper.StripMarkdownCodeFences()
└── Returns: ChatCompletionResponse (same as base, with possible Refusal)
```

## Validation Rules

| Entity | Rule | Error |
|--------|------|-------|
| JsonOutputOptions | SchemaName required when Mode == JsonSchema | `ArgumentException("SchemaName is required...")` |
| JsonOutputOptions | JsonSchema required when Mode == JsonSchema | `ArgumentException("JsonSchema is required...")` |
| JsonOutputOptions | JsonSchema must be valid JSON when Mode == JsonSchema | `ArgumentException("JsonSchema contains invalid JSON: ...")` |
| JsonOutputOptions | No validation for JsonMode | Succeeds regardless of schema fields |
| ImageDataUriHelper | File must exist | `FileNotFoundException` (I/O error, not API error) |
