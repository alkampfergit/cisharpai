# Data Model: Embeddings

## Unified Models (in `Cisharpai.Models`)

### EmbeddingRequest

Immutable record. Primary input for all text embedding operations.

| Field | Type | Required | Constraints | Notes |
|-------|------|----------|-------------|-------|
| Input | `IReadOnlyList<string>` | Yes | Non-empty | One or more text strings to embed |
| Model | `string?` | No | — | Falls back to provider options `DefaultModel`; throws if both null |
| InputType | `EmbeddingInputType?` | No | — | Hint for the provider (Cohere requires it; defaults to `Document`) |
| Dimensions | `int?` | No | > 0 | Dimension reduction (omitted from request when null) |
| EncodingFormat | `string?` | No | `"float"` or `"base64"` | Encoding of returned vectors |
| IncludeRawResponse | `bool` | No | Default `false` | Enables `RawResponseJson`/`RawRequestJson` capture |
| ExtraParameters | `JsonElement?` | No | — | Deep-merged into the provider request JSON |

### EmbeddingResponse

Immutable record. Unified output for all embedding operations (text, image, multimodal).

| Field | Type | Required | Constraints | Notes |
|-------|------|----------|-------------|-------|
| Embeddings | `IReadOnlyList<float[]>` | Yes | — | Float vectors, ordered by input index |
| Base64Embeddings | `IReadOnlyList<string>?` | No | — | Populated when `EncodingFormat: "base64"` |
| Model | `string` | Yes | — | Actual model name returned by provider |
| TotalTokens | `int` | Yes | ≥ 0 | Total token usage (Cohere sums `input_tokens + image_tokens`) |
| Dimensions | `int?` | No | — | Length of first vector (null for base64 or empty results) |
| RawResponseJson | `string?` | No | — | Populated when `IncludeRawResponse: true` |
| RawRequestJson | `string?` | No | — | Populated when `IncludeRawResponse: true` |
| IsSuccess | `bool` | Yes | Default `true` | `false` for API errors |
| ErrorMessage | `string?` | No | — | Populated when `IsSuccess == false` |

**Factory method**: `EmbeddingResponse.Error(errorMessage, rawResponseJson?)` creates a failed response with empty embeddings.

### EmbeddingInputType

Enum. Semantic hint for the provider about how the embeddings will be used.

| Value | Cohere Mapping | Azure AI Inference Mapping |
|-------|---------------|--------------------------|
| `Query` | `search_query` | `query` |
| `Document` | `search_document` | `document` |
| `Classification` | `classification` | `classification` |
| `Clustering` | `clustering` | `clustering` |

### MultimodalEmbeddingInput

Immutable record. Container for mixed-modality content parts.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Content | `IReadOnlyList<EmbeddingContentPart>` | Yes | One or more text/image parts |

### EmbeddingContentPart (abstract base)

| Subtype | Fields | Notes |
|---------|--------|-------|
| `TextEmbeddingContent` | `Text: string` | Plain text content |
| `ImageEmbeddingContent` | `ImagePath: string` | Local file path; converted to data URI at send time |

## Provider-Specific Models

### OpenAI (`Cisharpai.OpenAi.Models`)

**OpenAiEmbeddingRequest**: `Model`, `Input` (string or string[]), `EncodingFormat?`, `Dimensions?`, `User?`

**OpenAiEmbeddingResponse**: `Object`, `Data` (list of `{Object, Embedding: JsonElement, Index}`), `Model`, `Usage` (`PromptTokens`, `TotalTokens`)

### Azure OpenAI (`Cisharpai.Azure.AzureOpenAi.Models`)

**AzureOpenAiEmbeddingRequest**: `Input` (string or string[]), `EncodingFormat?`, `Dimensions?`, `User?`

**AzureOpenAiEmbeddingResponse**: Same shape as OpenAI.

### Azure AI Inference (`Cisharpai.Azure.AzureAiInference.Models`)

**AzureAiInferenceEmbeddingRequest**: `Model?`, `Input` (string[]), `Dimensions?`, `EncodingFormat?`, `InputType?`

**AzureAiInferenceImageEmbeddingRequest**: `Model?`, `Input` (list of `{Image: string}`)

**AzureAiInferenceEmbeddingResponse**: `Id`, `Object`, `Model`, `Data` (list of `{Index, Object, Embedding: JsonElement}`), `Usage?` (`PromptTokens`, `TotalTokens`)

### Cohere (`Cisharpai.Cohere.Models`)

**CohereEmbedRequest**: `Model`, `Texts?`, `Images?`, `Inputs?` (multimodal), `InputType`, `EmbeddingTypes`, `Truncate?`, `OutputDimension?`

**CohereEmbedResponse**: `Id`, `Embeddings` (`Float?`, `Int8?`, `Binary?`), `Texts`, `ResponseImages?`, `Meta` (`BilledUnits`: `InputTokens`, `Images?`, `ImageTokens?`)

**CohereEmbedInput**: `Content` (list of `CohereEmbedContentPart`)

**CohereEmbedContentPart**: `Type` ("text" or "image_url"), `Text?`, `ImageUrl?` (`Url: string`)

## Entity Relationships

```
EmbeddingRequest ──sends──▶ IEmbeddingClient ──returns──▶ EmbeddingResponse

MultimodalEmbeddingInput ──sends──▶ IMultimodalEmbeddingFeature ──returns──▶ EmbeddingResponse
  └── EmbeddingContentPart[]
       ├── TextEmbeddingContent
       └── ImageEmbeddingContent

imagePath (string) ──sends──▶ IImageEmbeddingFeature ──returns──▶ EmbeddingResponse
```
