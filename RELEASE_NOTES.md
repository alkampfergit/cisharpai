# Release Notes

This file is included in the NuGet packages. Keep a single line for each user-facing feature added to the library.

## Unreleased

- Prompt-injection grounded-chat fallback: `AzureAiInferenceChatCompletionClient` (and any future provider without native citations) now implements `IGroundedChatFeature` via `GroundedChatFallbackHelper` — documents are serialized into the system message with guillemet markers (`«cite:N»…«/cite»`), markers are parsed and stripped post-response, and `GroundingKind.Synthesized` signals callers that citations are prompt-injected rather than provider-native. All `CitationMode` values are accepted (no server-side distinction). Graceful degradation when the model emits no or malformed markers.
- New `GroundingKind` enum and `GroundedChatCompletionResponse.GroundingKind` property — defaults to `Native` for backward compatibility with existing providers; set to `Synthesized` by the fallback path.
- Anthropic grounded chat (RAG): `AnthropicChatCompletionClient` now implements `IGroundedChatFeature` — documents are sent as Anthropic `document` content blocks with native citations enabled, and responses map to the existing unified `Citation`/`CitationSource` model. Text documents use `text` source type; key-value `DocumentChunk.Data` uses `custom_content` source type. New optional `CitationSource.CitedText` field carries the source-document text span returned by Anthropic.
- OpenAI and Azure OpenAI grounded chat: `IGroundedChatFeature` now registered on both `OpenAiChatCompletionClient` and `AzureOpenAiChatCompletionClient` for GPT-5 models via the Responses API `input_file` transport; documents are base64-encoded as inline files, response annotations are mapped to `Citation`/`CitationSource` with `DocumentChunk.Id` preserved; non-GPT-5 models return `IsSuccess=false` with a descriptive error instead of silently falling back.
- Reranking: new `IRerankerClient` abstraction (`RerankRequest` / `RerankResponse` / `RerankResult`) with a Cohere implementation over `POST {BaseUrl}rerank` — register via `services.AddCohereRerankerClient(...)` (keyed overload available) or create at runtime through `ICisharpaiClientFactory.CreateRerankerClient`; set `CohereClientOptions.BaseUrl` to target Azure-hosted or self-hosted Cohere deployments, and reach Cohere's `priority` hint through `ExtraParameters`.
- `Cisharpai.Testing` gains `FakeRerankerClient`, `FakeResponses.Rerank`/`RerankError`, and `services.AddFakeRerankerClient()` for testing rerank-dependent code without HTTP calls.
- New `Cisharpai.Rag` package: Unicode-aware fixed-size chunking, bounded bulk float embeddings and document ingestion, with validated options, direct/DI/keyed-provider configuration, cancellation and explicit partial batch results.
- `Cisharpai.Rag` bulk embedding hardened for real corpora: dual-constraint batch sizing (item count + token budget), bounded concurrency with ordered output, transient failure retry with exponential backoff, `IProgress<BulkEmbeddingProgress>` observability, and failed batches no longer stop the run. **Breaking**: `BatchSize` renamed to `MaxBatchItems`.
- `Cisharpai.Rag` per-provider batch presets: `BulkEmbeddingOptions.ForProvider(EmbeddingProviderProfile.OpenAi)` (or `ApplyProfile` on existing options) sets the item and token ceilings for OpenAI, Azure OpenAI, Azure AI Inference and Cohere instead of the one global default of 32.
- `Cisharpai.Rag` concurrent bulk embedding now bounds read-ahead with `BulkEmbeddingOptions.MaxPendingBatches` (default `MaxConcurrency * 2`), so a slow batch or slow consumer can no longer buffer an entire corpus in memory.

## 0.3.0

- DI Client Factory: new `ICisharpaiClientFactory` interface for creating `IChatCompletionClient` / `IEmbeddingClient` at runtime from provider-agnostic configuration, with full resilience pipeline — register via `services.AddCisharpaiClientFactory().AddOpenAiSupport().AddAnthropicSupport()...`
- Azure OpenAI chat/json/tool-calling requests now recover from Azure routing mismatches by retrying the alternate endpoint or chat-completions token shape when the first guess is rejected, and learned mismatches are cached per `(Endpoint, DeploymentName, ApiVersion)` so later client instances reuse the working route.
- Cohere grounded chat: default `CitationMode` is now `Fast` (works on both `command-r` and `command-a` families); when `Accurate` is requested against a `command-a` model the provider logs a warning and silently downgrades to `Fast` instead of letting the API return HTTP 400.
- Azure OpenAI GPT-5 models now automatically route to the Responses API (same as OpenAI), with first-class `TextVerbosity` option on `AzureOpenAiClientOptions`.
- Azure OpenAI gains optional `ModelName` on `AzureOpenAiClientOptions` to drive routing when the deployment name is opaque (e.g. set `ModelName="gpt-5"` for a deployment named "foo"); when unset, routing falls back to the deployment/request model name and finally to standard Chat Completions.
- Azure OpenAI removes the temporary `ModelFamily` alias; use `ModelName` as the only explicit underlying-model hint.
- Azure OpenAI reasoning requests now support first-class `ReasoningEffort` configuration while still allowing `ExtraParameters` overrides.
- Azure OpenAI chat completions now return `IsSuccess=false` with `IncompleteReason="length"` when the provider reports `finish_reason: "length"`.
- Initial release notes file added to the NuGet package contents.
