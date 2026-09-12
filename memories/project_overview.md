# Project Structure Reference

This is the detailed structural reference for the Cisharpai project. For principles and rules, see `AGENTS.md`.

## Core Library — `src/Cisharpai/`

The only dependency needed by consuming applications.

- **`IChatCompletionClient.cs`** — Primary chat interface. Inherits `IHasFeatures`.
- **`IEmbeddingClient.cs`** — Primary embedding interface. Inherits `IHasFeatures`.
- **`IRerankerClient.cs`** — Primary reranking interface (`RerankAsync`). Inherits `IHasFeatures`. Cohere only.
- **`Features/`** — Feature Collection Pattern for optional capabilities:
  - `IFeatureCollection.cs` / `FeatureCollection.cs` — Thread-safe `Get<T>()`/`Set<T>()` backed by `ConcurrentDictionary`.
  - `Chat/IJsonOutputFeature.cs` — JSON Mode + Structured Outputs.
  - `Chat/IToolCallingFeature.cs` — Tool/function calling.
  - `Chat/IStreamingChatFeature.cs` — Token-by-token streaming via `IAsyncEnumerable<ChatCompletionChunk>`.
  - `Chat/IGroundedChatFeature.cs` — RAG with document citations.
  - `Embeddings/IImageEmbeddingFeature.cs` — Single image embedding.
  - `Embeddings/IMultimodalEmbeddingFeature.cs` — Mixed text+image embedding (Cohere Embed v4).
- **`Models/`** — Unified DTOs (all immutable records):
  - `ChatCompletionRequest` (Messages, Model?, Temperature, MaxTokens, ExtraParameters)
  - `ChatCompletionResponse` (Content, Usage, Status/IncompleteReason, IsSuccess/ErrorMessage, RawResponseJson/RawRequestJson, Refusal)
  - `EmbeddingRequest` / `EmbeddingResponse`
  - Reranking: `RerankRequest` (Query, Documents, Model?, TopN, MaxTokensPerDocument, ExtraParameters), `RerankResponse` (Results, Model, SearchUnits/InputTokens, IsSuccess/ErrorMessage, raw payloads), `RerankResult` (Index into the request documents, RelevanceScore)
  - `LlmMessage` (Role, Content, ContentParts, ToolCallId, ToolCalls) + factory methods `WithImage()`, `WithBase64Image()`
  - `MessageContentPart` hierarchy: `TextContentPart`, `ImageFileContentPart`, `ImageBase64ContentPart`
  - `ChatCompletionChunk` (streaming) with `ToolCallDelta`
  - Tool calling: `ToolDefinition`, `ToolCall`, `ToolResult`, `ToolChoice` (Auto/None/Required/Specific), `ToolCallingOptions`, `ToolCallingResponse`
  - JSON output: `JsonOutputMode`, `JsonOutputOptions`
  - Grounded chat: `DocumentChunk`, `Citation`, `CitationSource`, `CitationMode`, `GroundedChatOptions`, `GroundedChatCompletionResponse`
  - Multimodal: `MultimodalEmbeddingInput`, `EmbeddingContentPart`, `TextEmbeddingContent`, `ImageEmbeddingContent`
- **`Helpers/`** — Shared utilities: `JsonOutputHelper`, `RoleMapper`, `ContentPartHelper`, `ToolCallingHelper`, `EmbeddingHelper`
- **`JsonDeepMerge.cs`** — Deep-merges ExtraParameters JSON into request payloads.
- **`ImageDataUriHelper.cs`** — Converts image files to data URIs (PNG, JPEG, WebP, GIF).
- **`LlmHttpClient.cs`** — Internal HTTP helper with deep merge, raw request/response capture, SSE streaming.
- **Client Factory**:
  - `CisharpaiProvider.cs` — Enum identifying supported providers (OpenAi, AzureOpenAi, AzureAiInference, Anthropic, Cohere).
  - `CisharpaiClientConfiguration.cs` — Abstract base record (Provider + ApiKey); each provider defines a concrete subclass.
  - `CisharpaiClientFactoryResult<T>.cs` — Result wrapper (IsSuccess, Client, ErrorMessage) with static Success/Failure factories.
  - `IClientFactoryProvider.cs` — Provider descriptor interface; implemented per provider. `SupportsReranking` / `CreateRerankerClient` are **default interface members** (false / failure result) so existing implementations stay source-compatible.
  - `ICisharpaiClientFactory.cs` — Consumer-facing factory interface (CreateChatCompletionClient, CreateEmbeddingClient, CreateRerankerClient, GetRegisteredProviders).
  - `ICisharpaiClientFactoryBuilder.cs` — Fluent builder for registering providers.
  - `CisharpaiClientFactory.cs` — Default implementation; routes on Provider enum.
  - `CisharpaiClientFactoryExtensions.cs` — `services.AddCisharpaiClientFactory()` extension method.

## Providers

### `src/Cisharpai.OpenAi/`
- `OpenAiChatCompletionClient` — Implements chat + JSON output + tool calling + streaming. Routes by model: legacy (GPT-4), reasoning (o1/o3/o4), Responses API (GPT-5). Vision via data URI `image_url`. Static `Create(IHttpMessageHandlerFactory, options, ...)` for runtime construction.
- `OpenAiEmbeddingClient` — Text embeddings. Static `Create(IHttpMessageHandlerFactory, options, ...)` for runtime construction.
- `OpenAiModels` — Constants: `Chat.Gpt4_1`, `Chat.O3`, `Chat.O4Mini`, `Embedding.TextEmbedding3Small`, etc.
- `OpenAiClientOptions` — BaseUrl, ApiKey, Organization, ReasoningEffort, TextVerbosity, DefaultModel.
- `OpenAiClientConfiguration` — Factory config record (extends CisharpaiClientConfiguration).
- `OpenAiClientFactoryProvider` / `OpenAiFactoryBuilderExtensions` — Factory support.

### `src/Cisharpai.Azure/`
Consolidated package for all Azure AI services. Uses HttpClient directly (no SDK deps except Azure.Identity).

- **`Common/`** — `AzureClientOptionsBase`, `AzureAuthenticationHandler` (API key + Azure AD), `AzureErrorMapper`.
- **`AzureOpenAi/`** — `AzureOpenAiChatCompletionClient` (chat + JSON + tools + streaming), `AzureOpenAiEmbeddingClient`. Endpoint: `openai/deployments/{deployment}/...`. Three-way model routing (Legacy / Reasoning / Gpt5): gpt-5 deployments use the Responses API at `.../responses?api-version=...`; o-series uses Chat Completions with `reasoning_effort`; everything else is standard Chat Completions. Options: `DeploymentName`, `DefaultModel`, `ReasoningEffort`, `TextVerbosity` (gpt-5 Responses API), `ModelName` (explicit routing hint when the deployment name is opaque). Learned route mismatches are cached in-process per `(Endpoint, DeploymentName, ApiVersion)` so new client instances reuse the discovered route. Both have static `Create(IHttpMessageHandlerFactory, options, TokenCredential?, ...)`.
- **`AzureAiInference/`** — `AzureAiInferenceChatCompletionClient` (chat + JSON + tools + streaming), `AzureAiInferenceEmbeddingClient` (+ `IImageEmbeddingFeature`). Endpoint: `models/...`. Options: `ModelId`. Both have static `Create(IHttpMessageHandlerFactory, options, TokenCredential?, ...)`.
- **`Extensions/`** — DI registration with keyed service overloads. `AzureFactoryBuilderExtensions` for factory support.
- **Factory configs**: `AzureOpenAiClientConfiguration`, `AzureAiInferenceClientConfiguration` + corresponding factory providers.

### `src/Cisharpai.Anthropic/`
- `AnthropicChatCompletionClient` — Chat + JSON output (via `output_config.format`) + tool calling (`tool_use`/`tool_result` blocks) + streaming (event-based SSE). Vision uses raw base64 (NOT data URIs). Static `Create(IHttpMessageHandlerFactory, options, ...)` for runtime construction.
- `AnthropicModels` — Constants: `Chat.ClaudeOpus4_5`, `Chat.ClaudeSonnet4_5`, `Chat.ClaudeHaiku4_5`, etc.
- `AnthropicClientOptions` — BaseUrl, ApiKey, ApiVersion, DefaultModel.
- `AnthropicClientConfiguration` — Factory config record (chat only, no embedding).
- `AnthropicClientFactoryProvider` / `AnthropicFactoryBuilderExtensions` — Factory support.

### `src/Cisharpai.Cohere/`
- `CohereChatCompletionClient` — Chat + JSON + grounded chat (RAG) + tool calling + streaming. Vision: image parts silently skipped. Static `Create(IHttpMessageHandlerFactory, options, ...)` for runtime construction.
- `CohereEmbeddingClient` — Text + image + multimodal (Embed v4) embeddings. Images sent as data URIs. Static `Create(IHttpMessageHandlerFactory, options, ...)` for runtime construction.
- `CohereRerankerClient` — Reranking via `POST {BaseUrl}rerank`. Model from request or `DefaultModel` (throws if neither). `priority` reachable through `ExtraParameters`. Static `Create(IHttpMessageHandlerFactory, options, ...)` for runtime construction.
- `CohereModels` — Constants: `Chat.CommandA`, `Embedding.EmbedV4`, `Rerank.RerankV3_5`, etc.
- `CohereClientOptions` — BaseUrl, ApiKey, DefaultModel.
- `CohereClientConfiguration` — Factory config record.
- `CohereClientFactoryProvider` / `CohereFactoryBuilderExtensions` — Factory support.
- `CohereServiceCollectionExtensions` — DI with keyed overloads.

## RAG Ingestion — `src/Cisharpai.Rag/`

- Independently consumable library targeting .NET 8 and .NET 10; depends on core abstractions, not a specific provider.
- `Models/` — `RagDocument`, `TextChunk`, `ChunkEmbedding`, `EmbeddingBatchResult`; chunk identities and UTF-16 source offsets survive embedding.
- `Chunking/` — `ITextChunker`, `FixedSizeChunker`, `FixedSizeChunkerOptions`; lazy scalar-aware chunks (size 1024, overlap 128).
- `Embeddings/` — `IBulkEmbeddingProcessor`, `BulkEmbeddingProcessor`, `BulkEmbeddingOptions`; dual-constraint batching (item count + token budget), bounded concurrency (1–32 parallel requests with ordered output), transient failure retry with exponential backoff, `IProgress<BulkEmbeddingProgress>` observability, and continue-on-failure semantics.
- `Models/BulkEmbeddingProgress` — progress record: `CompletedBatches`, `TotalChunksProcessed`, `FailedBatches`.
- `IRagIngestionPipeline` / `RagIngestionPipeline` — compose document chunking with bulk embedding; collection and async-stream overloads, cancellation, progress pass-through, and partial batch results.
- `RagOptions` and `AddCisharpaiRag` — validated option snapshots, callback configuration and embedding-client factory for keyed DI; scoped processors/pipelines.
- No storage, retrieval or tokenization. Token estimation uses a pluggable `Func<string, int>` seam (default: `s.Length / 4`). Existing embedding fakes support offline tests without core interface changes.
- Consumer guide: `wiki/rag.md`; unit tests: `src/Cisharpai.Tests/Rag/`.

## Testing Package — `src/Cisharpai.Testing/`

- `FakeChatCompletionClient` — Fake for `IChatCompletionClient` + all chat features. Response queues, defaults, request capture.
- `FakeEmbeddingClient` — Fake for `IEmbeddingClient` + embedding features.
- `FakeRerankerClient` — Fake for `IRerankerClient`. Response queue, default, request capture, `Reset()`. No feature flags (no optional rerank features exist).
- `FakeResponses` — Static factories: `Chat`, `ChatError`, `ToolCall`, `ToolCalls`, `GroundedChat`, `StreamingChunks`, `Embedding`, `Rerank`, `RerankError`, etc.
- `FakeChatFeatures` / `FakeEmbeddingFeatures` — `[Flags]` enums for selective feature registration.
- `FakeServiceCollectionExtensions` — DI helpers.

## Console App — `src/Cisharp.Console/`

Interactive Spectre.Console demo with scenarios for each provider/capability. Entry: `Program.cs`, scenarios in `Scenarios/`.

## Tests

### `src/Cisharpai.Tests/` (Unit)
Organized by provider folder: `OpenAi/`, `Anthropic/`, `Cohere/`, `Azure/AzureOpenAi/`, `Azure/AzureAiInference/`, `Azure/Common/`.
Also: `Models/`, `Features/`, `DependencyInjection/`, `Core/`, `Testing/`, `Rag/`.

### `src/Cisharpai.Tests.Common/`
- `DotEnvLoader` — Loads `.env` files.
- `TestEnvironmentVariables` — Constants for env var names.

### `src/Cisharpai.Integration.Tests/` (Integration — .NET 10 only)
Real API tests organized by provider folder. `EnvironmentConfigurationTests` validates all required env vars.

## Wiki — `wiki/`

Pages: `index.md`, `getting-started.md`, `openai.md`, `embeddings.md`, `rag.md`, `feature-extensions.md`, `json-output.md`, `grounded-chat.md`, `provider-features.md`, `tool-calling.md`, `vision.md`, `streaming.md`, `testing.md`.

## CI/CD

- `.github/workflows/ci.yml` — Build + unit tests + integration tests. .NET 8 & 10.
- `.github/workflows/pipeline.yml` — Versioned build + NuGet publish on tags.
- `.github/workflows/codeql.yml` — CodeQL security scanning.
- `scripts/build.ps1` — PowerShell build script: restore, build, test, pack 7 projects.
- `GitVersion.yml` — ContinuousDeployment mode.
