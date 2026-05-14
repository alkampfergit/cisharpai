# Research: Embeddings

## Decision 1: Shared `EmbeddingHelper` for OpenAI-compatible response mapping

**Decision**: A static `EmbeddingHelper.MapEmbeddingResponse()` method handles the common case of ordered embedding data items with `JsonElement` embedding values.

**Rationale**: OpenAI, Azure OpenAI, and Azure AI Inference all return the same response shape (`data[].embedding`, `data[].index`). Extracting this into a helper avoids duplicating the sort-by-index + parse-float/base64 logic in three places.

**Alternatives considered**:
- **Per-provider mapping only**: Would duplicate ~30 lines of identical code across 3 clients. Rejected for DRY violation.
- **Shared base class**: Would couple providers through inheritance. Rejected in favor of composition via static helper.

**Note**: Cohere has a different response shape (`embeddings.float[][]`) and does not use this helper.

## Decision 2: `JsonElement` for embedding values instead of `float[]`

**Decision**: Provider response models store embedding vectors as `JsonElement` rather than `float[]`.

**Rationale**: The embedding value can be either a float array or a base64 string depending on the `encoding_format` parameter. Using `JsonElement` allows deferring the type decision until mapping time, avoiding a discriminated union or two separate fields in the provider model.

**Alternatives considered**:
- **`object` field**: Loses type information; error-prone deserialization.
- **Two fields (`float[]?` + `string?`)**: More explicit but doubles the data model surface for a rarely-used option.

## Decision 3: Single input as string, batch as array (OpenAI/Azure OpenAI)

**Decision**: When `Input` contains a single string, it is serialized as a JSON string. When it contains multiple strings, it is serialized as a JSON array.

**Rationale**: The OpenAI API accepts both forms, but sending a single string avoids a redundant array wrapper. This matches the official SDK behavior and reduces payload size for the common single-input case.

**Alternatives considered**:
- **Always send array**: Simpler code but creates a minor serialization mismatch with the canonical API examples.

**Note**: Azure AI Inference always sends an array regardless of count, matching its API documentation.

## Decision 4: Cohere defaults `InputType` to `search_document`

**Decision**: When `EmbeddingInputType` is null, Cohere maps it to `"search_document"`.

**Rationale**: Cohere's API requires `input_type` on every request. Defaulting to `search_document` matches the most common use case (indexing documents for retrieval) and avoids requiring callers to always specify it.

**Alternatives considered**:
- **Throw on null**: Would break the unified interface contract (other providers don't require it).
- **Send null**: Cohere API rejects requests without `input_type`.

## Decision 5: Separate endpoints for text vs. image embeddings (Azure AI Inference)

**Decision**: Text embeddings use `models/embeddings`, image embeddings use `models/images/embeddings`.

**Rationale**: Azure AI Inference has physically separate endpoints for text and image embeddings with different request schemas. The text endpoint takes `input: string[]`, while the image endpoint takes `input: [{image: "data:..."}]`.

**Alternatives considered**:
- **Single endpoint with polymorphic input**: Not supported by the Azure AI Inference API.

## Decision 6: Cohere uses three mutually exclusive input formats

**Decision**: `CohereEmbedRequest` has three nullable fields — `Texts`, `Images`, and `Inputs` — of which exactly one is populated per request.

**Rationale**: Cohere's embed API evolved across versions: v3 uses `texts` for text and `images` for single-image; v4 uses `inputs` for multimodal. Rather than separate request types, a single request class with nullable fields keeps the client simple and matches the API's single-endpoint design.

**Alternatives considered**:
- **Three separate request classes**: Would require three separate HTTP paths or a discriminated builder, adding complexity for a single-endpoint API.

## Decision 7: Token counting includes image tokens for Cohere

**Decision**: `TotalTokens` in the unified response is computed as `input_tokens + image_tokens` for Cohere.

**Rationale**: Cohere reports text and image tokens separately in `billed_units`. Since `EmbeddingResponse.TotalTokens` is a single integer representing total usage, summing both gives callers an accurate cost metric without requiring them to understand Cohere's billing model.

**Alternatives considered**:
- **Expose separate text/image token fields**: Would add Cohere-specific fields to the unified model, violating Principle I.
