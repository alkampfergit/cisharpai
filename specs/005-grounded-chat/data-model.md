# Data Model: Grounded Chat

## Unified Models (in `Cisharpai.Models`)

### GroundedChatOptions

Immutable record. Configures document grounding for a chat request.

| Field | Type | Required | Default | Constraints |
|-------|------|----------|---------|-------------|
| Documents | `IReadOnlyList<DocumentChunk>` | Yes | — | Non-empty; each chunk must pass `Validate()` |
| CitationMode | `CitationMode` | No | `Fast` | `Accurate` downgrades to `Fast` on `command-a` models |

**Validation**: `Validate()` checks Documents is non-null and non-empty, then delegates to each `DocumentChunk.Validate()`.

### DocumentChunk

Immutable record. A single document provided for grounding.

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| Id | `string?` | No | Omitted from request when null |
| Data | `IReadOnlyDictionary<string, string>?` | No* | Mutually exclusive with Text; must be non-empty when present |
| Text | `string?` | No* | Mutually exclusive with Data; must be non-whitespace when present |

*Exactly one of `Data` or `Text` must be set.

**Validation**: `Validate()` throws `ArgumentException` if:
- Neither Data nor Text is provided
- Both Data and Text are provided
- Data is an empty dictionary
- Text is empty or whitespace-only

### GroundedChatCompletionResponse

Immutable record. Wraps a standard chat response with citations.

| Field | Type | Notes |
|-------|------|-------|
| ChatCompletion | `ChatCompletionResponse` | Full chat response (content, tokens, raw JSON, etc.) |
| Citations | `IReadOnlyList<Citation>` | Empty list when no citations generated |

**Convenience properties** (delegate to `ChatCompletion`):
- `IsSuccess` → `ChatCompletion.IsSuccess`
- `ErrorMessage` → `ChatCompletion.ErrorMessage`
- `Content` → `ChatCompletion.Content`

**Factory**: `Error(errorMessage, rawResponseJson?)` creates a failed response with empty citations.

### Citation

Immutable record. A cited span in the response content.

| Field | Type | Notes |
|-------|------|-------|
| Start | `int` | Inclusive start character offset |
| End | `int` | Exclusive end character offset |
| Text | `string` | The cited text span |
| Sources | `IReadOnlyList<CitationSource>` | Backing source documents |
| Type | `string?` | Provider-specific type (e.g., `"TEXT_CONTENT"` for Cohere) |

### CitationSource

Immutable record. A source document backing a citation.

| Field | Type | Notes |
|-------|------|-------|
| Id | `string` | Document identifier |
| Data | `IReadOnlyDictionary<string, string>?` | Original document data (if structured) |

### CitationMode

Enum.

| Value | Provider Mapping | Notes |
|-------|-----------------|-------|
| `Accurate` | `"ACCURATE"` | Fine-grained; only `command-r` family |
| `Fast` | `"FAST"` | Inline; all Cohere models (default) |
| `Enabled` | `"ENABLED"` | Provider-default behavior |

## Provider-Specific Models (in `Cisharpai.Cohere.Models`)

### CohereChatDocument

| Field | Type | Notes |
|-------|------|-------|
| Id | `string?` | Omitted when null via JSON serialization |
| Data | `JsonElement` | Serialized from `Dictionary<string, string>` or plain string |

### CohereCitationOptions

| Field | Type |
|-------|------|
| Mode | `string` | `"ACCURATE"`, `"FAST"`, or `"ENABLED"` |

### CohereChatCitation / CohereChatCitationSource

Provider response models mapping 1:1 with Cohere's JSON structure.

## Entity Relationships

```
ChatCompletionRequest + GroundedChatOptions ──▶ IGroundedChatFeature
                                                       │
                                                       ▼
                                          GroundedChatCompletionResponse
                                            ├── ChatCompletionResponse
                                            └── Citation[]
                                                  └── CitationSource[]

GroundedChatOptions
  ├── DocumentChunk[] (1..N)
  └── CitationMode
```
