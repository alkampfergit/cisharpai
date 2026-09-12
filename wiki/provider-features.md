# Provider Feature Matrix

This page lists every feature supported by each provider integration in Cisharpai. Keep it up-to-date whenever a feature is added or removed.

## Feature Legend

| Feature | Interface | Description |
|---------|-----------|-------------|
| Chat Completions | `IChatCompletionClient` | Send messages and receive model-generated replies |
| Text Embeddings | `IEmbeddingClient` | Generate vector embeddings from text |
| Reranking | `IRerankerClient` | Reorder candidate documents by relevance to a query |
| RAG Ingestion | `IRagIngestionPipeline` (`Cisharpai.Rag`) | Fixed-size chunking and sequential bulk float embeddings |
| JSON Output | `IJsonOutputFeature` | Force JSON Mode or Structured Outputs on chat responses |
| Image Embeddings | `IImageEmbeddingFeature` | Generate vector embeddings from a single image |
| Multimodal Embeddings | `IMultimodalEmbeddingFeature` | Embed mixed text + image inputs in a single request |
| Grounded Chat (RAG) | `IGroundedChatFeature` | Chat with document grounding and citations |
| Tool Calling | `IToolCallingFeature` | Function calling / tool use in chat completions |
| Vision (Image Input) | `LlmMessage.ContentParts` | Send images inline in chat messages for visual understanding |
| Streaming | `IStreamingChatFeature` | Stream chat completions token-by-token via SSE |
| Client Factory | `ICisharpaiClientFactory` | Create chat/embedding/reranker clients at runtime from provider-agnostic configuration |

## Support Matrix

| Feature | OpenAI | Azure OpenAI | Azure AI Inference | Anthropic | Cohere |
|---------|--------|--------------|-------------------|-----------|--------|
| Chat Completions | Yes | Yes | Yes | Yes | Yes |
| Text Embeddings | Yes | Yes | Yes | -- | Yes |
| Reranking | -- | -- | -- | -- | Yes |
| RAG Ingestion | Yes | Yes | Yes | -- | Yes |
| JSON Mode | Yes | Yes | Yes | Yes | Yes |
| Structured Outputs | Yes | Yes | Yes | Yes | Yes |
| Image Embeddings | -- | -- | Yes | -- | Yes |
| Multimodal Embeddings | -- | -- | -- | -- | Yes |
| Reasoning Models | Yes | Yes | Yes | -- | -- |
| Responses API (GPT-5) | Yes | Yes | -- | -- | -- |
| Grounded Chat (RAG) | -- | -- | -- | -- | Yes |
| Tool Calling | Yes | Yes | Yes | Yes | Yes |
| Vision (Image Input) | Yes | Yes | Yes | Yes | Partial* |
| Streaming | Yes | Yes | Yes | Yes | Yes |
| Logging & Tracing | Yes | Yes | Yes | Yes | Yes |
| Client Factory | Yes | Yes | Yes | Yes | Yes |

\* Cohere Vision: image content parts are silently skipped (only text extracted). Cohere chat API does not support visual inputs.

**Logging & Tracing:** every provider client routes through `LlmHttpClient`, which emits structured `ILogger` entries (EventIds 1000–1005) and `System.Diagnostics.Activity` spans from the `Cisharpai` source (constant: `Cisharpai.CisharpaiTelemetry.ActivitySourceName`). See [Logging](logging.md) for the property/tag set and subscription options.

**RAG ingestion:** `Cisharpai.Rag` composes any `IEmbeddingClient`; it is a separate library, not a discovered provider feature. Provider model, dimensions and request limits still apply. It provides no vector storage or retrieval. See [RAG Ingestion](rag.md).

## Provider Details

### OpenAI

**Package:** `Cisharpai.OpenAi`

| Capability | Details |
|------------|---------|
| Chat Completions | Legacy (GPT-4), Reasoning (o1/o3/o4), Responses API (GPT-5) |
| Text Embeddings | text-embedding-3-small, text-embedding-3-large, text-embedding-ada-002 |
| JSON Mode | Via `response_format` (Chat Completions API) or `text.format` (Responses API) |
| Structured Outputs | Via `response_format.json_schema`; refusal extraction supported |
| Reasoning Models | o1, o3, o3-mini, o4-mini — uses `max_completion_tokens` |
| Responses API | GPT-5 models — status and incomplete-reason tracking |
| Tool Calling | All models; `ToolChoice` supports Auto, None, Required, Specific (function name) |
| Vision | Send images via `LlmMessage.WithImage()` or `LlmMessage.WithBase64Image()`; images are sent as data URIs (`data:image/{mime};base64,...`) |
| Streaming | `IStreamingChatFeature`; legacy Chat Completions API and Responses API (GPT-5); `[DONE]` terminates the stream |

### Azure OpenAI

**Package:** `Cisharpai.Azure`

| Capability | Details |
|------------|---------|
| Chat Completions | Deployment-based routing; three-way model detection (Legacy GPT-4 / Reasoning o1-o3-o4 / GPT-5) with one-shot recovery when Azure rejects the first route guess |
| Text Embeddings | text-embedding-ada-002, text-embedding-3-small, text-embedding-3-large |
| JSON Mode | Via `response_format` (Chat Completions, requires api-version 2024-08-01-preview+ for json_schema) or `text.format` (Responses API for GPT-5) |
| Structured Outputs | Via `response_format.json_schema` (Chat Completions) or `text.format` with `json_schema` (Responses API); refusal extraction supported |
| Reasoning Models | o1/o3/o4 detected automatically; uses `max_completion_tokens` instead of `max_tokens`; `AzureOpenAiClientOptions.ReasoningEffort` sends `reasoning_effort` for o-series and gpt-5 deployments |
| Responses API (GPT-5) | gpt-5 deployments route to `openai/deployments/{name}/responses?api-version=...`; `AzureOpenAiClientOptions.TextVerbosity` maps to `text.verbosity`; if Azure returns `404 Resource not found`, the client retries Chat Completions once and shares the learned route across later client instances with the same endpoint, deployment, and API version |
| Model Name Override | `AzureOpenAiClientOptions.ModelName` forces routing for opaque deployment names (e.g. `DeploymentName="foo"` + `ModelName="gpt-5"` routes to the Responses API) |
| Tool Calling | All deployments; identical JSON shape to OpenAI (`tools` array, `tool_choice` parameter); all `ToolChoice` variants supported. GPT-5 tool calling uses Chat Completions (matches OpenAI client). |
| Vision | Same data URI format as OpenAI; images sent as content parts in messages |
| Streaming | `IStreamingChatFeature`; supports legacy, reasoning, and Responses API streams; `[DONE]` terminates Chat Completions streams; gpt-5 uses `response.completed` |
| Authentication | API key (`api-key` header) or Azure AD (Bearer token) |

`ReasoningEffort` is omitted for non-reasoning Azure OpenAI deployments to avoid unsupported-parameter errors. `TextVerbosity` is sent only when the model is detected as gpt-5. When Azure rejects `max_tokens` for a reasoning deployment, the client retries once with `max_completion_tokens`; when the initially selected endpoint is wrong, it retries the alternate endpoint once. Learned mismatches are cached in-process per `(Endpoint, DeploymentName, ApiVersion)` for future Azure OpenAI client instances. `ExtraParameters` still deep-merges into the final request and can override either typed option or add newer Azure/OpenAI parameters before the typed options are updated.

Azure OpenAI truncation is surfaced as a failed unified response when the provider returns `finish_reason: "length"`: `IsSuccess=false`, `Status="length"`, and `IncompleteReason="length"`. Some reasoning deployments may instead return an Azure HTTP error when the output limit is too low; those remain `IsSuccess=false` with the provider error in `ErrorMessage`/`RawResponseJson`.

### Azure AI Inference

**Package:** `Cisharpai.Azure`

| Capability | Details |
|------------|---------|
| Chat Completions | Phi-3, Llama-3, Mistral, and other model-catalog offerings |
| Text Embeddings | Provider-agnostic embedding endpoint |
| Image Embeddings | Single image via `IImageEmbeddingFeature` |
| JSON Mode | Via `response_format`; availability varies by deployed model |
| Structured Outputs | Via `response_format.json_schema`; availability varies by deployed model |
| Reasoning Models | o1/o3/o4/gpt-5 detected automatically |
| Tool Calling | Model-dependent; uses OpenAI-compatible `tools` array and `tool_choice`; all `ToolChoice` variants supported |
| Vision | Same data URI format as OpenAI; availability depends on deployed model |
| Streaming | `IStreamingChatFeature`; supports both standard and reasoning request formats; `[DONE]` terminates the stream |
| Authentication | API key (`api-key` header) or Azure AD (Bearer token) |

### Anthropic

**Package:** `Cisharpai.Anthropic`

| Capability | Details |
|------------|---------|
| Chat Completions | Claude model family (claude-opus-4-5, claude-sonnet-4-5, claude-haiku-4-5) |
| JSON Mode | Implemented via system-message injection; auto-strips markdown fences |
| Structured Outputs | Via native `output_config.format` parameter; refusal via `stop_reason: "refusal"` |
| Tool Calling | All Claude models; `ToolChoice` maps Auto->auto, Required->any, Specific->{type:tool,name}, None is omitted |
| Vision | Images sent as raw base64 (NOT data URIs) via `source.type: "base64"` in content blocks |
| Streaming | `IStreamingChatFeature`; event-based SSE (no `[DONE]` sentinel); `message_start`/`content_block_delta`/`message_delta` events |

### Cohere

**Package:** `Cisharpai.Cohere`

| Capability | Details |
|------------|---------|
| Chat Completions | Command family models (command-a-03-2025, command-r-plus-08-2024, command-r-08-2024) |
| Text Embeddings | Embed v3 and v4 models |
| Reranking | `IRerankerClient` via `POST {BaseUrl}rerank`; rerank-v3.5, rerank-english-v3.0, rerank-multilingual-v3.0; `TopN` and `MaxTokensPerDocument` supported; `priority` reachable via `ExtraParameters`; `BaseUrl` retargets to Azure AI Foundry or other hosts |
| JSON Mode | Via `response_format` type `json_object` |
| Structured Outputs | Via `response_format` with `json_schema` parameter |
| Image Embeddings | Single image via data URI (`data:image/{mime};base64,...`) |
| Multimodal Embeddings | Embed v4 mixed text + image inputs, Matryoshka dimension control, batch images |
| Supported Formats | PNG, JPEG, WebP, GIF |
| Grounded Chat (RAG) | Document grounding via `documents` array, `citation_options` mode (ACCURATE/FAST/ENABLED), citation character offsets |
| Input Types | search_query, search_document, classification, clustering |
| Tool Calling | Command models; `ToolChoice` maps to uppercase (AUTO/NONE/REQUIRED); Specific degrades to REQUIRED; `strict_tools` flag when all tools are strict |
| Vision | Partial: image content parts are silently skipped (only text extracted). Cohere chat does not support images. Use Cohere Embed v4 for image embeddings. |
| Streaming | `IStreamingChatFeature`; event-based SSE with `content-delta` and `message-end` events; finish_reason uses uppercase (COMPLETE/MAX_TOKENS) |

## Feature Discovery

All optional features are accessed through the Feature Collection pattern:

```csharp
IChatCompletionClient chatClient = /* any provider */;

// JSON output
if (chatClient.Features.Get<IJsonOutputFeature>() is { } jsonFeature)
{
    var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);
}

IEmbeddingClient embedClient = /* any provider */;

// Image embeddings
if (embedClient.Features.Get<IImageEmbeddingFeature>() is { } imageFeature)
{
    var response = await imageFeature.GetImageEmbeddingAsync(imagePath, model);
}

// Multimodal embeddings
if (embedClient.Features.Get<IMultimodalEmbeddingFeature>() is { } multimodalFeature)
{
    var response = await multimodalFeature.GetMultimodalEmbeddingsAsync(inputs, model);
}

// Grounded chat (RAG)
if (chatClient.Features.Get<IGroundedChatFeature>() is { } groundedFeature)
{
    var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);
    // response.Citations contains source references
}

// Tool calling (function calling)
if (chatClient.Features.Get<IToolCallingFeature>() is { } toolFeature)
{
    var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);
    // response.ToolCalls contains requested tool invocations
}
```

See [Feature Extensions](feature-extensions.md) for the full Feature Collection documentation.
