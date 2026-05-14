# Feature Specification: Embeddings (Text, Image, Multimodal)

**Feature Branch**: `feature/gh-specify`

**Created**: 2026-05-14

**Status**: Complete (Retrospec)

**Input**: User description: "all the embedding features for images multimodal etc, everything regarding embeddings"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate Text Embeddings via Any Provider (Priority: P1)

As a developer, I want to generate vector embeddings from text through a single unified interface so that I can build semantic search, similarity, and clustering applications without coupling to a specific provider.

**Why this priority**: Text embeddings are the core value proposition of the embedding subsystem. Without a unified text embedding interface, there is no product.

**Independent Test**: Create any provider's embedding client, send a single text input, and verify the response contains a float array, model name, token usage, and dimension count.

**Acceptance Scenarios**:

1. **Given** an OpenAI embedding client configured with an API key and model, **When** I send an `EmbeddingRequest` with a single text input, **Then** I receive an `EmbeddingResponse` with non-empty `Embeddings`, populated `Model`, `TotalTokens` > 0, and `Dimensions` matching the vector length.
2. **Given** an Azure OpenAI embedding client configured with endpoint and deployment, **When** I send the same request shape, **Then** I receive the same response shape.
3. **Given** an Azure AI Inference embedding client configured with endpoint and model ID, **When** I send the same request shape, **Then** I receive the same response shape.
4. **Given** a Cohere embedding client configured with an API key, **When** I send the same request shape with an `InputType`, **Then** I receive the same response shape.
5. **Given** any embedding client, **When** I send a request with multiple text inputs (batch), **Then** the response contains one embedding vector per input, in order.

---

### User Story 2 - Customize Embedding Parameters (Priority: P1)

As a developer, I want to control embedding dimensions, encoding format, and input type so that I can optimize for storage, performance, and use case.

**Why this priority**: Dimension reduction and encoding options are essential for production use cases (e.g., reducing vector DB storage costs).

**Independent Test**: Send a request with `Dimensions: 256` to an OpenAI client and verify the returned vector has exactly 256 elements.

**Acceptance Scenarios**:

1. **Given** an embedding request with `Dimensions: 256`, **When** sent to a provider that supports dimension reduction, **Then** the response vectors have exactly 256 elements.
2. **Given** an embedding request without `Dimensions`, **When** the parameter is serialized, **Then** it is omitted from the provider request (not sent as null).
3. **Given** an embedding request with `EncodingFormat: "base64"`, **When** sent to a provider that supports base64 encoding, **Then** the response populates `Base64Embeddings` instead of `Embeddings`.
4. **Given** a Cohere embedding request with `InputType: Query`, **When** serialized, **Then** the provider receives `input_type: "search_query"`.
5. **Given** a Cohere embedding request without `InputType`, **When** serialized, **Then** the provider receives `input_type: "search_document"` as default.

---

### User Story 3 - Embed Single Images (Priority: P2)

As a developer, I want to generate embeddings from images so that I can build visual similarity search and cross-modal retrieval applications.

**Why this priority**: Image embeddings extend the embedding subsystem to visual data, enabling multimodal search. Only available on providers with vision-capable embedding models.

**Independent Test**: Discover the `IImageEmbeddingFeature` on an Azure AI Inference or Cohere client, send a local image file path, and verify the response contains a float vector.

**Acceptance Scenarios**:

1. **Given** an Azure AI Inference embedding client, **When** I discover `IImageEmbeddingFeature` via `client.Features.Get<IImageEmbeddingFeature>()`, **Then** the feature is available.
2. **Given** a valid local image path, **When** I call `GetImageEmbeddingAsync(imagePath, model)`, **Then** the image is converted to a data URI and sent to the provider, and I receive an `EmbeddingResponse` with a float vector.
3. **Given** a Cohere embedding client, **When** I call `GetImageEmbeddingAsync`, **Then** the image is sent as a data URI in the `images` array with `input_type: "image"`.
4. **Given** an empty image path, **When** I call `GetImageEmbeddingAsync`, **Then** an `ArgumentException` is thrown.
5. **Given** an OpenAI or Azure OpenAI embedding client, **When** I check for `IImageEmbeddingFeature`, **Then** it is not available (returns null).

---

### User Story 4 - Embed Mixed Text and Images (Multimodal) (Priority: P2)

As a developer, I want to embed mixed text-and-image inputs in a single request so that I can build applications that understand the relationship between textual and visual content.

**Why this priority**: Multimodal embeddings (Cohere Embed v4) are a differentiating capability for advanced search and retrieval use cases.

**Independent Test**: Discover `IMultimodalEmbeddingFeature` on a Cohere client, send a mix of `TextEmbeddingContent` and `ImageEmbeddingContent` parts, and verify the response contains embeddings.

**Acceptance Scenarios**:

1. **Given** a Cohere embedding client, **When** I discover `IMultimodalEmbeddingFeature`, **Then** the feature is available.
2. **Given** a multimodal input with text-only content, **When** sent via `GetMultimodalEmbeddingsAsync`, **Then** the provider receives the `inputs` array format (not `texts`).
3. **Given** a multimodal input with an image, **When** sent, **Then** the image file is converted to a data URI and sent as `type: "image_url"` with a nested `image_url.url` field.
4. **Given** a multimodal input with both text and image parts, **When** sent, **Then** all parts appear in order in the `content` array of the provider request.
5. **Given** batch multimodal inputs (multiple `MultimodalEmbeddingInput` items), **When** sent, **Then** the response contains one embedding vector per input.
6. **Given** `outputDimension: 256`, **When** included in the request, **Then** the provider receives `output_dimension: 256` (Matryoshka dimension control).
7. **Given** `outputDimension: null`, **When** serialized, **Then** the `output_dimension` field is omitted from the request.

---

### User Story 5 - Debug and Inspect Raw Payloads (Priority: P2)

As a developer, I want to inspect the raw JSON request and response payloads for embedding operations so that I can diagnose provider-specific issues and verify correct serialization.

**Why this priority**: Debuggability is a core project principle. Raw payload access reduces time-to-resolution for integration issues.

**Independent Test**: Send an embedding request with `IncludeRawResponse: true` and verify `RawResponseJson` and `RawRequestJson` are populated.

**Acceptance Scenarios**:

1. **Given** an embedding request with `IncludeRawResponse: true`, **When** the request succeeds, **Then** `response.RawResponseJson` contains the provider's JSON response and `response.RawRequestJson` contains the serialized request.
2. **Given** an embedding request without `IncludeRawResponse`, **When** the request succeeds, **Then** `response.RawResponseJson` and `response.RawRequestJson` are both null.
3. **Given** a multimodal embedding request with `includeRawResponse: true`, **When** the request succeeds, **Then** raw JSON is populated on the response.

---

### User Story 6 - Handle Errors Gracefully (Priority: P2)

As a developer, I want embedding errors to be returned as failed responses (not exceptions) so that I can handle failures consistently via property checks.

**Why this priority**: No-exceptions-for-API-errors is a non-negotiable project principle.

**Independent Test**: Send a request that triggers an HTTP 500 error and verify `IsSuccess == false` with a populated `ErrorMessage`.

**Acceptance Scenarios**:

1. **Given** a provider returns HTTP 500, **When** an embedding request is made, **Then** `response.IsSuccess` is `false`, `response.ErrorMessage` contains the status code, and `response.Embeddings` is empty.
2. **Given** a provider returns HTTP 401, **When** an embedding request is made, **Then** `response.IsSuccess` is `false` and `response.RawResponseJson` contains the error body.
3. **Given** a provider returns an error for an image embedding request, **When** `GetImageEmbeddingAsync` is called, **Then** the response has `IsSuccess == false`.
4. **Given** a provider returns an error for a multimodal embedding request, **When** `GetMultimodalEmbeddingsAsync` is called, **Then** the response has `IsSuccess == false`.

---

### User Story 7 - Test with Fake Embedding Clients (Priority: P3)

As a developer consuming the library, I want fake embedding clients with canned responses, queues, and request capture so that I can unit-test my application without real API calls.

**Why this priority**: The testing package is a deliverable per the project constitution, but downstream consumer testing is a secondary concern after the feature itself works.

**Independent Test**: Create a `FakeEmbeddingClient`, enqueue a canned response, call `GetEmbeddingsAsync`, and verify the canned response is returned and the request is captured.

**Acceptance Scenarios**:

1. **Given** a `FakeEmbeddingClient` with a queued `EmbeddingResponse`, **When** `GetEmbeddingsAsync` is called, **Then** the queued response is returned and `ReceivedRequests` contains the request.
2. **Given** a `FakeEmbeddingClient` with `DefaultResponse` set, **When** called with no queued responses, **Then** the default is returned.
3. **Given** a `FakeEmbeddingClient` with `FakeEmbeddingFeatures.ImageEmbedding` enabled, **When** I discover `IImageEmbeddingFeature`, **Then** it is available and captures image requests.
4. **Given** a `FakeEmbeddingClient` with `FakeEmbeddingFeatures.None`, **When** I check for `IImageEmbeddingFeature`, **Then** it is not available.
5. **Given** a `FakeEmbeddingClient` with multimodal enabled, **When** `GetMultimodalEmbeddingsAsync` is called, **Then** the inputs are captured in `ReceivedMultimodalRequests`.

---

### Edge Cases

- What happens when a provider does not return a `model` field? (Azure AI Inference falls back to the configured `ModelId`)
- What happens when Cohere returns `image_tokens` alongside `input_tokens`? (Total tokens sums both)
- What happens when a single text input is sent? (OpenAI/Azure OpenAI serialize as string, not array; Azure AI Inference always sends array)
- What happens when no model is specified and no default is configured? (Throws `InvalidOperationException`)
- What happens when embedding data items arrive out of order? (Responses are sorted by `index` before mapping)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a unified `IEmbeddingClient` interface for generating text embeddings across all supported providers.
- **FR-002**: System MUST support batch embedding (multiple text inputs in a single request) and return one vector per input in order.
- **FR-003**: System MUST support optional dimension reduction via a `Dimensions` parameter.
- **FR-004**: System MUST support optional encoding format selection (float or base64).
- **FR-005**: System MUST support input type hints (query, document, classification, clustering) for providers that require them.
- **FR-006**: System MUST support single image embedding via an optional `IImageEmbeddingFeature` discoverable through the Feature Collection pattern.
- **FR-007**: System MUST support multimodal (mixed text + image) embedding via an optional `IMultimodalEmbeddingFeature` discoverable through the Feature Collection pattern.
- **FR-008**: System MUST convert local image file paths to data URIs automatically (PNG, JPEG, WebP, GIF).
- **FR-009**: System MUST support Matryoshka dimension control for multimodal embeddings via an `outputDimension` parameter.
- **FR-010**: System MUST return `IsSuccess=false` with `ErrorMessage` for all API errors (no exceptions for HTTP error responses).
- **FR-011**: System MUST support raw request/response JSON capture via `IncludeRawResponse` flag.
- **FR-012**: System MUST support `ExtraParameters` deep-merge for provider-specific options not yet modeled in the unified request.
- **FR-013**: System MUST provide fake embedding clients (`FakeEmbeddingClient`) with response queues, defaults, request capture, and selective feature registration for downstream unit testing.
- **FR-014**: System MUST sort embedding data items by provider-returned index to guarantee order matches input order.
- **FR-015**: System MUST support model resolution from request, then options default, and throw if neither is specified.

### Key Entities

- **EmbeddingRequest**: Unified input — text inputs, model, dimensions, encoding format, input type, extra parameters, raw response flag.
- **EmbeddingResponse**: Unified output — float vectors, optional base64 vectors, model name, token usage, dimensions, success/error state, raw JSON.
- **EmbeddingInputType**: Semantic hint for the provider about how the embeddings will be used.
- **MultimodalEmbeddingInput**: Container for mixed content parts (text and/or image) in a single embedding input.
- **EmbeddingContentPart**: Abstract base for content within a multimodal input (TextEmbeddingContent, ImageEmbeddingContent).
- **IImageEmbeddingFeature**: Optional feature interface for single-image embedding.
- **IMultimodalEmbeddingFeature**: Optional feature interface for mixed text+image embedding.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All four text embedding providers (OpenAI, Azure OpenAI, Azure AI Inference, Cohere) produce valid `EmbeddingResponse` objects from the same `EmbeddingRequest` shape.
- **SC-002**: Image embedding works on Azure AI Inference and Cohere; feature discovery returns null on non-supporting providers.
- **SC-003**: Multimodal embedding works on Cohere with mixed text+image inputs and Matryoshka dimension control.
- **SC-004**: All embedding operations return `IsSuccess=false` (not exceptions) for HTTP error responses.
- **SC-005**: Raw JSON capture is available on all embedding operations when opted in.
- **SC-006**: `FakeEmbeddingClient` supports all three embedding modes with queues, defaults, and request capture.
- **SC-007**: All unit tests pass on both .NET 8.0 and .NET 10.

## Assumptions

- All providers are accessed via HTTP REST APIs using `HttpClient` (no provider SDKs).
- Image files are read from the local filesystem and encoded as base64 data URIs.
- Cohere is the only provider supporting multimodal (mixed text+image) embeddings as of this implementation.
- Azure AI Inference and Cohere are the only providers supporting image embeddings.
- The `EmbeddingInputType` enum covers the four use-case categories defined by Cohere; other providers may ignore it.
- Anthropic does not offer an embedding API and is excluded from this feature.

## Retrospec Metadata

**Generated**: 2026-05-14
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 30 files across 7 projects
**Reference implementation branch**: feature/gh-specify
