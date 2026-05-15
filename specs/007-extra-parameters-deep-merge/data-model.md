# Data Model: ExtraParameters & Deep Merge

## Request Properties

### ChatCompletionRequest (augmented)

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `ExtraParameters` | `JsonElement?` | `null` | Arbitrary JSON object deep-merged into the serialized request body |
| `IncludeRawResponse` | `bool` | `false` | When true, response includes `RawRequestJson` and `RawResponseJson` |

### EmbeddingRequest (augmented)

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `ExtraParameters` | `JsonElement?` | `null` | Arbitrary JSON object deep-merged into the serialized request body |
| `IncludeRawResponse` | `bool` | `false` | When true, response includes `RawRequestJson` and `RawResponseJson` |

## Response Properties

### ChatCompletionResponse (augmented)

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `RawResponseJson` | `string?` | `null` | Exact JSON received from the provider API |
| `RawRequestJson` | `string?` | `null` | Exact JSON sent to the provider API (post-merge) |

### EmbeddingResponse (augmented)

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `RawResponseJson` | `string?` | `null` | Exact JSON received from the provider API |
| `RawRequestJson` | `string?` | `null` | Exact JSON sent to the provider API (post-merge) |

## Static Utility: JsonDeepMerge

Static class. Performs recursive merge of two JSON objects.

### `Merge(string baseJson, JsonElement overrides) → string`

**Input**:
- `baseJson`: Serialized JSON string (must be a JSON object)
- `overrides`: `JsonElement` (must be `ValueKind == Object`)

**Output**: Merged JSON string

**Merge Rules**:

| Base Value | Override Value | Result |
|------------|---------------|--------|
| Object | Object | Recursive merge (both sides' properties preserved) |
| Object | Scalar/Array/Null | Override replaces base |
| Scalar | Object | Override replaces base |
| Scalar | Scalar | Override replaces base |
| Array | Array | Override replaces base (no element merge) |
| Any | Null | Property set to JSON `null` |
| Present | Absent | Base value preserved |
| Absent | Present | Override value added |

**Property ordering**: Base properties appear first (in original order), then override-only properties.

**Validation**: Throws `ArgumentException` if either `baseJson` or `overrides` is not a JSON object.

## Infrastructure: LlmHttpClient.SerializeAndMerge

Private method on `LlmHttpClient`.

```
SerializeAndMerge<TRequest>(TRequest payload, JsonElement? extraParameters) → string
```

1. Serializes `payload` to JSON using `System.Text.Json` with camelCase naming and null-ignoring.
2. If `extraParameters` has a value and is a JSON object, calls `JsonDeepMerge.Merge`.
3. Returns the final JSON string.

Called by all three HTTP methods:
- `PostAsync<TRequest, TResponse>` — standard requests
- `PostWithRawAsync<TRequest, TResponse>` — requests with raw JSON capture
- `PostStreamAsync<TRequest>` — streaming SSE requests

## Data Flow

```
ChatCompletionRequest
  ├── ExtraParameters: JsonElement?
  └── IncludeRawResponse: bool
        │
        ▼
  Provider Client (OpenAI, Azure, Anthropic, Cohere)
    builds provider-specific request DTO
    passes request.ExtraParameters to LlmHttpClient
        │
        ▼
  LlmHttpClient.SerializeAndMerge()
    serialize DTO → JSON string
    if ExtraParameters != null → JsonDeepMerge.Merge(json, extra)
        │
        ▼
  HTTP POST (merged JSON body)
        │
        ▼
  LlmHttpClient.PostWithRawAsync (if IncludeRawResponse)
    returns (result, rawResponseJson, rawRequestJson)
        │
        ▼
  ChatCompletionResponse
    ├── RawRequestJson: merged payload
    └── RawResponseJson: provider response
```
