# Requirements Checklist: Embeddings

## Functional Requirements

- [x] **FR-001**: Unified `IEmbeddingClient` interface for text embeddings across all providers
  - Implemented: `src/Cisharpai/IEmbeddingClient.cs` with 4 provider implementations
- [x] **FR-002**: Batch embedding (multiple inputs, one vector per input, ordered)
  - Verified: `OpenAiEmbeddingClientTests.GetEmbeddingsAsync_BatchTextInput_MapsAllEmbeddings`, `CohereEmbeddingClientTests.GetEmbeddingsAsync_BatchInput_MapsAllEmbeddings`
- [x] **FR-003**: Optional dimension reduction via `Dimensions` parameter
  - Verified: `OpenAiEmbeddingClientTests.GetEmbeddingsAsync_PassesDimensionsWhenProvided`, `GetEmbeddingsAsync_OmitsDimensionsWhenNull`
- [x] **FR-004**: Optional encoding format selection (float or base64)
  - Implemented: `EmbeddingHelper.MapEmbeddingResponse` handles both formats; `EmbeddingRequest.EncodingFormat` passed to all providers
- [x] **FR-005**: Input type hints (query, document, classification, clustering)
  - Verified: `CohereEmbeddingClientTests.GetEmbeddingsAsync_SetsCorrectInputType` (4 parametrized cases), `AzureAiInferenceEmbeddingClient.MapInputType`
- [x] **FR-006**: Single image embedding via `IImageEmbeddingFeature`
  - Implemented: `AzureAiInferenceEmbeddingClient`, `CohereEmbeddingClient` — both register the feature in their `FeatureCollection`
- [x] **FR-007**: Multimodal embedding via `IMultimodalEmbeddingFeature`
  - Implemented: `CohereEmbeddingClient` — registers the feature; handles text, image, and mixed inputs
- [x] **FR-008**: Automatic local image file to data URI conversion
  - Implemented: `ImageDataUriHelper.ToDataUriAsync()` called by both Azure AI Inference and Cohere image embedding paths
- [x] **FR-009**: Matryoshka dimension control via `outputDimension`
  - Verified: `CohereMultimodalEmbeddingTests.GetMultimodalEmbeddingsAsync_OutputDimension_SentInRequest`, `OutputDimensionNull_OmittedFromRequest`
- [x] **FR-010**: No exceptions for API errors — returns `IsSuccess=false`
  - Verified: `OpenAiEmbeddingClientTests.GetEmbeddingsAsync_HttpError_ReturnsErrorResponse`, similar tests in all provider test classes
- [x] **FR-011**: Raw request/response JSON capture via `IncludeRawResponse`
  - Verified: `OpenAiEmbeddingClientTests.GetEmbeddingsAsync_IncludeRawResponse_ReturnsRawJson`, `GetEmbeddingsAsync_WithoutIncludeRawResponse_RawJsonIsNull`
- [x] **FR-012**: `ExtraParameters` deep-merge
  - Implemented: All providers pass `request.ExtraParameters` to `LlmHttpClient.PostAsync`/`PostWithRawAsync`
- [x] **FR-013**: Fake embedding client with queues, defaults, capture, selective features
  - Implemented: `FakeEmbeddingClient` with `FakeEmbeddingFeatures` flags enum
- [x] **FR-014**: Embedding data sorted by provider-returned index
  - Implemented: `.OrderBy(d => d.Index)` in OpenAI, Azure OpenAI, and Azure AI Inference response mapping
- [x] **FR-015**: Model resolution from request → options default → throw
  - Verified: OpenAI and Cohere clients throw `InvalidOperationException` when both are null

## User Stories

- [x] **US-1**: Generate text embeddings via any provider (P1)
- [x] **US-2**: Customize embedding parameters (P1)
- [x] **US-3**: Embed single images (P2)
- [x] **US-4**: Embed mixed text and images — multimodal (P2)
- [x] **US-5**: Debug and inspect raw payloads (P2)
- [x] **US-6**: Handle errors gracefully (P2)
- [x] **US-7**: Test with fake embedding clients (P3)

## Constitution Compliance

- [x] Unified Abstraction — single interface, Feature Collection for optional capabilities
- [x] No Exceptions for API Errors — all providers return error responses
- [x] Debuggability First — raw JSON + ExtraParameters
- [x] Immutability — all DTOs are immutable records
- [x] Test-Driven Quality — 6 unit test classes covering all providers and modes
- [x] Multi-Target Compatibility — projects target net8.0;net10
- [x] Documentation as Deliverable — `wiki/embeddings.md` covers all providers

## Documentation

- [x] `wiki/embeddings.md` — full usage guide with all providers
- [x] `wiki/provider-features.md` — feature matrix includes embedding columns
- [x] `memories/project_overview.md` — embedding clients and models documented
