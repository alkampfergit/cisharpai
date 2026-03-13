# Project Overview

## Description
**Cisharpai** is a unified .NET client library designed to provide a common interface for interacting with various Large Language Model (LLM) providers. It abstracts the differences between provider APIs (OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere), allowing developers to switch providers with minimal code changes.

## Foundation Principles & Design Guidelines

The design of Cisharpai is built on several key architectural decisions intended to balance simplicity with power:

1.  **Unified Abstraction**: The core goal is to allow applications to depend solely on `Cisharpai` (the core library). Provider-specific logic is encapsulated in separate packages, ensuring that switching from OpenAI to Anthropic is largely a configuration change.
2.  **"Escape Hatch" Extensibility**: We recognize that AI providers move faster than strongly-typed libraries can update. The `ExtraParameters` property (and the "Deep Merge" strategy) allows users to inject arbitrary JSON properties into requests. This ensures developers can use bleeding-edge features (like new sampling parameters) immediately without waiting for a library release.
3.  **No Exceptions for API Errors**: Clients do not throw exceptions for API failures (4xx/5xx). Instead, they return a response object where `IsSuccess` is false and `ErrorMessage` contains details. This promotes robust error handling flows. Exceptions are reserved for network transport issues or gross misuse (like missing configurations).
4.  **Debuggability First**: AI interactions can be opaque. All response objects allow access to `RawResponseJson` and `RawRequestJson` (when requested), enabling developers to see exactly what was sent and received over the wire.
5.  **Immutability**: Data transfer objects (Requests/Responses) are immutable `records`, promoting thread safety and predictable state management.
6.  **Feature Collection Pattern**: Optional capabilities (e.g., image embedding) are discovered via `IHasFeatures.Features.Get<T>()`, similar to ASP.NET Core's `HttpContext.Features`. This avoids polluting core interfaces with provider-specific methods while enabling type-safe capability discovery.

## Project Structure

### Core Library (`src/Cisharpai/`)
Contains the abstractions and shared logic. This is the only dependency needed for the consuming application logic.

*   **`IChatCompletionClient.cs`**: The primary interface for performing chat completion requests. Inherits `IHasFeatures` for capability discovery.
*   **`IEmbeddingClient.cs`**: The primary interface for generating text embeddings. Inherits `IHasFeatures` for capability discovery.
*   **`Features/`**: Extension system for optional capability discovery (Feature Collection Pattern, similar to ASP.NET Core `HttpContext.Features`).
    *   `IHasFeatures.cs`: Marker interface exposing an `IFeatureCollection Features` property.
    *   `IFeatureCollection.cs`: Type-safe container with `Get<T>()` and `Set<T>()` for feature lookup/registration.
    *   `FeatureCollection.cs`: Thread-safe implementation backed by `ConcurrentDictionary<Type, object>`.
    *   `Embeddings/IImageEmbeddingFeature.cs`: Optional feature for embedding images via `GetImageEmbeddingAsync(imagePath, model)`.
    *   `Embeddings/IMultimodalEmbeddingFeature.cs`: Optional feature for multimodal embedding (text + images in a single request) via `GetMultimodalEmbeddingsAsync(inputs, model, inputType, outputDimension, ...)`. Supports Cohere Embed v4 mixed-modality inputs and Matryoshka dimension control.
    *   `Chat/IJsonOutputFeature.cs`: Optional feature for JSON output enforcement on chat completion clients via `GetChatCompletionWithJsonOutputAsync(request, jsonOutputOptions)`. Supports JSON Mode (`json_object`) and Structured Outputs (`json_schema`). Registered on OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, and Cohere clients.
    *   `Chat/IGroundedChatFeature.cs`: Optional feature for grounded chat (RAG) with document citations via `GetGroundedChatCompletionAsync(request, groundedChatOptions)`. Passes documents and returns citations with character offsets. Currently registered on Cohere client only.
    *   `Chat/IToolCallingFeature.cs`: Optional feature for tool calling (function calling) in chat completions via `GetChatCompletionWithToolsAsync(request, toolCallingOptions)`. Registered on OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, and Cohere clients.
    *   `Chat/IStreamingChatFeature.cs`: Optional feature for streaming chat completions token-by-token via `GetChatCompletionStreamAsync(request)` returning `IAsyncEnumerable<ChatCompletionChunk>`. Registered on all 5 chat clients.
*   **`Models/`**:
    *   **`ChatCompletionRequest.cs`**: Unified request model (Messages, Model?, Temperature, MaxTokens, IncludeRawResponse, ExtraParameters). `Model` is optional (`string?`, defaults to `null`); when omitted, the provider client falls back to `DefaultModel` from its options. The `ExtraParameters` property (`JsonElement?`) allows passing arbitrary JSON that is deeply merged into the provider-specific request body, enabling use of new model features without DTO changes.
    *   **`ChatCompletionResponse.cs`**: Unified response model (Content, Usage stats, optional Status/IncompleteReason for Responses API, IsSuccess/ErrorMessage for error handling, RawResponseJson/RawRequestJson for debug inspection, optional Refusal for Structured Outputs safety refusals). Provider clients never throw exceptions; errors are returned via `IsSuccess = false` and `ErrorMessage`. Includes a static `Error()` factory method.
    *   **`JsonOutputMode.cs`**: Enum defining JSON output modes: `JsonMode` (json_object, valid JSON without schema enforcement) and `JsonSchema` (json_schema, strict schema-conforming output).
    *   **`JsonOutputOptions.cs`**: Configuration record for JSON output behavior (Mode, SchemaName, SchemaDescription, JsonSchema, Strict). Includes `Validate()` method that throws `ArgumentException` when `JsonSchema` mode is used without required schema fields.
    *   **`EmbeddingRequest.cs`**: Unified request model for embedding operations (Input, Model?, InputType, Dimensions, EncodingFormat, ExtraParameters). `Model` is optional (`string?`, defaults to `null`); when omitted, the provider client falls back to `DefaultModel` from its options.
    *   **`EmbeddingResponse.cs`**: Unified response model for embeddings (Embeddings, Base64Embeddings, Model, TotalTokens, Dimensions, RawResponseJson, RawRequestJson, IsSuccess/ErrorMessage).
    *   **`MultimodalEmbeddingInput.cs`**: Models for multimodal embedding. `EmbeddingContentPart` (abstract base), `TextEmbeddingContent(Text)`, `ImageEmbeddingContent(ImagePath)`, and `MultimodalEmbeddingInput(Content)` for composing mixed text+image inputs.
    *   **`CitationMode.cs`**: Enum defining citation modes for grounded chat: `Accurate`, `Fast`, `Enabled`.
    *   **`DocumentChunk.cs`**: Represents a document chunk for RAG (Id, Data as key-value dict, or Text as plain string). Includes `Validate()` ensuring exactly one of Data/Text is set.
    *   **`CitationSource.cs`**: Represents a source document backing a citation (Id, optional Data dictionary).
    *   **`Citation.cs`**: Represents a citation in a grounded response (Start/End character offsets, Text, Sources, optional Type for provider-specific citation type e.g. Cohere's "TEXT_CONTENT").
    *   **`GroundedChatOptions.cs`**: Configuration record for grounded chat (Documents, CitationMode). Includes `Validate()`.
    *   **`GroundedChatCompletionResponse.cs`**: Wraps `ChatCompletionResponse` with `Citations`. Convenience `IsSuccess`, `Content`, `ErrorMessage` properties. Static `Error()` factory.
    *   **`ToolDefinition.cs`**: Defines a tool available for the model (Name, Description, Parameters as JsonElement, Strict flag defaulting to true). Includes `Validate()` method.
    *   **`ToolCall.cs`**: Represents a tool invocation requested by the model (Id, FunctionName, Arguments as JsonElement).
    *   **`ToolResult.cs`**: Represents the result of executing a tool (ToolCallId, Content, IsError flag).
    *   **`ToolChoice.cs`**: Controls tool selection strategy. Sealed abstract record with hierarchy: `AutoChoice`, `NoneChoice`, `RequiredChoice`, `SpecificChoice(Name)`. Static factories: `Auto`, `None`, `Required`, `Specific(functionName)`.
    *   **`ToolCallingOptions.cs`**: Configuration record for tool calling (Tools as IReadOnlyList<ToolDefinition>, ToolChoice). Includes `Validate()` method.
    *   **`ToolCallingResponse.cs`**: Wraps `ChatCompletionResponse` with optional `ToolCalls` (IReadOnlyList<ToolCall>?). Convenience properties for IsSuccess, Content, ErrorMessage. Static `Error()` factory.
    *   **`LlmMessage.cs`**: Represents a message in the conversation (Role, Content, optional ToolCallId, optional ToolCalls, optional ContentParts for multimodal messages). Factory methods: `WithImage(text, filePath)` and `WithBase64Image(text, base64Data, mediaType)`.
    *   **`MessageContentPart.cs`**: Abstract base `MessageContentPart` record and concrete sealed types: `TextContentPart(string Text)`, `ImageFileContentPart(string FilePath)`, `ImageBase64ContentPart(string Base64Data, string MediaType)`. Used in `LlmMessage.ContentParts` for multimodal vision messages.
    *   **`ChatCompletionChunk.cs`**: Streaming chunk model with `Content`, `FinishReason`, `Model`, `PromptTokens`, `CompletionTokens`, `ToolCallDelta`. `ToolCallDelta` has `Index`, `Id`, `FunctionName`, `ArgumentsDelta` for streaming tool calls.
*   **`JsonDeepMerge.cs`**: Static utility for deeply merging a JSON override document into a base JSON document. Objects are merged recursively; arrays and scalars are replaced by overrides.
*   **`ImageDataUriHelper.cs`**: Public static utility for converting image file paths to data URI format (`data:image/{mime};base64,...`). Supports PNG, JPEG, WebP, GIF. Used by OpenAI/Azure providers for vision.
*   **`LlmHttpClient.cs`**: Internal helper for handling HTTP requests to the providers. Supports optional `extraParameters` (`JsonElement?`) that are deeply merged into the serialized request payload before sending. `PostWithRawAsync` returns both raw response JSON and raw request JSON for debug inspection. `PostStreamAsync<TRequest>` supports SSE streaming (handles `data:` prefix, `[DONE]` termination, ignores `event:` lines, works with both `[DONE]`-terminated and naturally-ending streams).
*   **`Helpers/`**: Shared public static helper utilities that eliminate code duplication across provider implementations.
    *   `JsonOutputHelper.cs`: `EnsureJsonKeywordInSystemMessage(messages, options, suffix)` — ensures system message mentions JSON for JsonMode; `StripMarkdownCodeFences(content)` — strips ````json ... ```` fences from output. Used by all 5 chat clients.
    *   `RoleMapper.cs`: `MapRole(LlmRole)` — maps LlmRole to standard string ("system"/"user"/"assistant"/"tool"). Used by OpenAI, Azure OpenAI, Azure AI Inference, Cohere (not Anthropic, which has different mapping).
    *   `ContentPartHelper.cs`: `ExtractStringContent(object?)` — extracts string from response content that may be raw string or JsonElement. Used by OpenAI, Azure OpenAI, Azure AI Inference.
    *   `ToolCallingHelper.cs`: `MapResponseToolCalls<T>(toolCalls, extractor)` — generic tool call response mapping with provider-specific extractor; `MapToolChoice(toolChoice, specificMapper)` — maps ToolChoice to string/object with provider-specific Specific handler; `MapStreamToolCallDelta<T>(toolCalls, extractor)` — maps streaming tool call deltas. Used by OpenAI, Azure OpenAI, Azure AI Inference, Cohere.
    *   `EmbeddingHelper.cs`: `MapEmbeddingResponse(orderedEmbeddings, encodingFormat, model, totalTokens, ...)` — maps ordered embedding JsonElements to unified EmbeddingResponse with base64/float support. Used by OpenAI and Azure OpenAI embedding clients.

### Provider Implementations
Each supported provider has its own project providing concrete implementations of the core interfaces.

*   **`src/Cisharpai.OpenAi/`**: Connector for standard OpenAI API. Supports legacy Chat Completions API (GPT-4, etc.), reasoning models (o1/o3/o4), and the Responses API (GPT-5) with status/incomplete handling.
    *   `OpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient`, `IJsonOutputFeature`, `IToolCallingFeature`, and `IStreamingChatFeature`. Routes to the correct endpoint/format based on model detection. Supports JSON Mode and Structured Outputs across legacy, reasoning, and GPT-5 (Responses API) model paths. Extracts refusal from structured output responses. Supports tool calling with all ToolChoice variants including Specific. Supports vision via `ContentParts` (image_url with data URIs for file paths, base64 data). Streaming uses SSE with `[DONE]` termination; GPT-5 Responses API uses `response.output_text.delta` and `response.completed` events.
    *   `Models/OpenAiContentPart.cs`: DTOs for vision: `OpenAiContentPart` (base), `OpenAiTextContentPart(text)`, `OpenAiImageContentPart(image_url)`, `OpenAiImageUrl(url)`.
    *   `Models/OpenAiStreamEvent.cs`: SSE response DTOs for streaming: `OpenAiStreamChunk`, `OpenAiStreamChoice`, `OpenAiStreamDelta`, `OpenAiStreamToolCall`.
    *   `OpenAiEmbeddingClient.cs`: Implements `IEmbeddingClient`.
    *   `OpenAiModels.cs`: Static class with well-known model ID constants. Nested `Chat` class (Gpt4_1, Gpt4_1Mini, Gpt4_1Nano, Gpt4o, Gpt4oMini, Gpt4_5, O3, O3Mini, O3Pro, O4Mini, O1, O1Mini) and `Embedding` class (TextEmbedding3Small, TextEmbedding3Large, TextEmbeddingAda002).
    *   `OpenAiClientOptions.cs`: Configuration with BaseUrl, ApiKey, Organization, ReasoningEffort, TextVerbosity, and `DefaultModel` (optional, used when `ChatCompletionRequest.Model` is null).
    *   `Models/OpenAiResponseFormat.cs`: DTOs for `response_format` parameter: `OpenAiResponseFormat`, `OpenAiJsonSchemaSpec` (Chat Completions API), `OpenAiTextFormat` (Responses API with flattened schema structure).
    *   `Models/OpenAiToolDefinition.cs`: DTOs for tool calling: `OpenAiToolDefinition`, `OpenAiToolFunction`, `OpenAiToolChoiceFunction`, `OpenAiToolChoiceObject`, `OpenAiToolCallFunction`, `OpenAiToolCall`.
*   **`src/Cisharpai.Azure/`**: Consolidated connector for all Azure AI services. Uses HttpClient directly (no SDK dependencies except Azure.Identity for authentication).
    *   **`Common/`**: Shared utilities for all Azure services.
        *   `AzureClientOptionsBase.cs`: Base class for Azure client configuration (Endpoint, ApiKey, ApiVersion).
        *   `AzureAuthenticationHandler.cs`: DelegatingHandler supporting both API key (`api-key` header) and Azure AD (Bearer token) authentication. Uses scope `https://cognitiveservices.azure.com/.default`.
        *   `AzureErrorMapper.cs`: Static utility for mapping HTTP status codes to user-friendly error messages.
    *   **`AzureOpenAi/`**: Connector for Azure OpenAI Service. Supports both legacy models and reasoning/GPT-5 models (uses `max_completion_tokens` instead of `max_tokens`).
        *   `AzureOpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient`, `IJsonOutputFeature`, `IToolCallingFeature`, and `IStreamingChatFeature` with Azure-specific auth/routing. Detects reasoning models (o1/o3/o4/gpt-5) and uses appropriate request format. Supports JSON Mode and Structured Outputs. Supports tool calling with all ToolChoice variants including Specific. Supports vision via `ContentParts` (data URI image_url). Streaming uses SSE with `[DONE]` termination; reasoning models use `max_completion_tokens`. Endpoint: `openai/deployments/{deployment}/chat/completions?api-version=...`.
        *   `Models/AzureOpenAiContentPart.cs`: DTOs for vision: `AzureOpenAiContentPart`, `AzureOpenAiTextContentPart`, `AzureOpenAiImageContentPart`, `AzureOpenAiImageUrl`.
        *   `Models/AzureOpenAiStreamEvent.cs`: SSE response DTOs for streaming.
        *   `Models/AzureOpenAiResponseFormat.cs`: DTOs for `response_format` parameter: `AzureOpenAiResponseFormat`, `AzureOpenAiJsonSchemaSpec`.
        *   `Models/AzureOpenAiToolDefinition.cs`: DTOs for tool calling: `AzureOpenAiToolDefinition`, `AzureOpenAiToolFunction`, `AzureOpenAiToolChoiceFunction`, `AzureOpenAiToolChoiceObject`, `AzureOpenAiToolCallFunction`, `AzureOpenAiToolCall`.
        *   `AzureOpenAiEmbeddingClient.cs`: Implements `IEmbeddingClient`. Supports text-embedding-ada-002, text-embedding-3-small, text-embedding-3-large deployments. Endpoint: `openai/deployments/{deployment}/embeddings?api-version=...`.
        *   `AzureOpenAiClientOptions.cs`: Configuration with DeploymentName and `DefaultModel` (optional, used for model-type detection when `ChatCompletionRequest.Model` is null), extends AzureClientOptionsBase. Default API version: `2024-10-21`.
        *   `Models/`: Request/response DTOs for Azure OpenAI API.
    *   **`AzureAiInference/`**: Connector for Azure AI Inference (model-as-a-service). Supports Phi-3, Llama-3, Mistral, and other Azure AI model catalog offerings, including reasoning models (o1/o3/o4/GPT-5). Uses HttpClient directly (not the Azure.AI.Inference SDK).
        *   `AzureAiInferenceChatCompletionClient.cs`: Implements `IChatCompletionClient`, `IJsonOutputFeature`, `IToolCallingFeature`, and `IStreamingChatFeature`. Detects reasoning models (o1/o3/o4/gpt-5) and uses appropriate request format (`max_completion_tokens` instead of `max_tokens`, no `Temperature`). Supports JSON Mode and Structured Outputs. Supports tool calling with all ToolChoice variants including Specific. Supports vision via `ContentParts` (data URI image_url). Streaming uses SSE with `[DONE]` termination. Endpoint: `models/chat/completions?api-version=...`.
        *   `Models/AzureAiInferenceContentPart.cs`: DTOs for vision: `AzureAiInferenceContentPart`, `AzureAiInferenceTextContentPart`, `AzureAiInferenceImageContentPart`, `AzureAiInferenceImageUrl`.
        *   `Models/AzureAiInferenceStreamEvent.cs`: SSE response DTOs for streaming.
        *   `Models/AzureAiInferenceResponseFormat.cs`: DTOs for `response_format` parameter: `AzureAiInferenceResponseFormat`, `AzureAiInferenceJsonSchemaSpec`.
        *   `Models/AzureAiInferenceToolDefinition.cs`: DTOs for tool calling: `AzureAiInferenceToolDefinition`, `AzureAiInferenceToolFunction`, `AzureAiInferenceToolChoiceFunction`, `AzureAiInferenceToolChoiceObject`, `AzureAiInferenceToolCallFunction`, `AzureAiInferenceToolCall`.
        *   `AzureAiInferenceEmbeddingClient.cs`: Implements `IEmbeddingClient` and `IImageEmbeddingFeature`. Supports text and image embeddings. Endpoint: `models/embeddings?api-version=...`.
        *   `AzureAiInferenceClientOptions.cs`: Configuration with ModelId, extends AzureClientOptionsBase. Default API version: `2024-05-01-preview`.
        *   `Models/`: Request/response DTOs for Azure AI Inference API.
    *   **`Extensions/`**: DI service collection extensions. Each extension method owns its options via closure (not registered in DI), enabling independent configuration when both chat and embedding clients are registered. Keyed overloads support .NET 8 keyed services.
        *   `AzureOpenAiServiceCollectionExtensions.cs`: `AddAzureOpenAiClient()` and `AddAzureOpenAiEmbeddingClient()` for registering Azure OpenAI clients. Keyed overloads available.
        *   `AzureAiInferenceServiceCollectionExtensions.cs`: `AddAzureAiInferenceChatCompletion()` and `AddAzureAiInferenceEmbeddings()` for registering Azure AI Inference clients. Keyed overloads available.
*   **`src/Cisharpai.Anthropic/`**: Connector for Anthropic (Claude) API. Supports structured outputs via `output_config.format` parameter.
    *   `AnthropicChatCompletionClient.cs`: Implements `IChatCompletionClient`, `IJsonOutputFeature`, `IToolCallingFeature`, and `IStreamingChatFeature`. Supports JSON Mode (via system message injection) and Structured Outputs (`json_schema` via `output_config.format`). Handles refusal via `stop_reason: "refusal"`. Supports tool calling with tool_use/tool_result content blocks. Supports vision via `source` content blocks (raw base64 NOT data URIs). Streaming uses event-based SSE (`message_start`, `content_block_delta`, `message_delta`) — no `[DONE]` sentinel.
    *   `AnthropicModels.cs`: Static class with well-known model ID constants. Nested `Chat` class (ClaudeOpus4_5, ClaudeSonnet4_5, ClaudeHaiku4_5, ClaudeSonnet4, ClaudeHaiku4, ClaudeOpus3).
    *   `AnthropicClientOptions.cs`: Configuration with BaseUrl, ApiKey, ApiVersion, and `DefaultModel` (optional, used when `ChatCompletionRequest.Model` is null).
    *   `Models/AnthropicOutputConfig.cs`: DTOs for `output_config.format` parameter: `AnthropicOutputConfig`, `AnthropicOutputFormat`.
    *   `Models/AnthropicToolDefinition.cs`: DTOs for tool calling: `AnthropicToolDefinition` (Name, Description, InputSchema), `AnthropicToolChoice` (Type, Name).
    *   `Models/AnthropicImageSource.cs`: DTOs for vision: `AnthropicImageSource` (type, media_type, data as raw base64), `AnthropicImageContentBlock`.
    *   `Models/AnthropicStreamEvent.cs`: SSE event DTOs for streaming: `AnthropicStreamEvent`, `AnthropicMessageStartData`, `AnthropicContentBlockDeltaData`, `AnthropicTextDelta`, `AnthropicMessageDeltaData`, `AnthropicMessageDeltaUsage`.
*   **`src/Cisharpai.Cohere/`**: Connector for Cohere API. Supports Embed v3/v4 models and Chat v2 API.
    *   `CohereChatCompletionClient.cs`: Implements `IChatCompletionClient`, `IJsonOutputFeature`, `IGroundedChatFeature`, `IToolCallingFeature`, and `IStreamingChatFeature`. Supports JSON Mode and Structured Outputs via `response_format` with `json_object` type and optional `json_schema`. Supports grounded chat (RAG) via `documents` array and `citation_options`. Supports tool calling with uppercase ToolChoice mapping and strict_tools flag. Uses system message injection for JSON Mode. Endpoint: `chat`. Vision: image content parts are silently skipped (Cohere chat does not support images); only text content parts are concatenated. Streaming uses `stream: true`; events: `stream-start`, `content-delta`, `message-end` (no `[DONE]`; stream ends naturally).
    *   `CohereEmbeddingClient.cs`: Implements `IEmbeddingClient`, `IImageEmbeddingFeature`, and `IMultimodalEmbeddingFeature`. Supports text embeddings, single image embedding, and Embed v4 multimodal embedding (mixed text+image inputs, Matryoshka output dimensions, batch images). Images are sent as data URIs.
    *   `CohereModels.cs`: Static class with well-known model ID constants. Nested `Chat` class (CommandA, CommandRPlus, CommandR) and `Embedding` class (EmbedV4, EmbedEnglishV3, EmbedMultilingualV3, EmbedEnglishLightV3, EmbedMultilingualLightV3).
    *   `CohereClientOptions.cs`: Configuration with BaseUrl, ApiKey, and `DefaultModel` (optional, used when `ChatCompletionRequest.Model` or `EmbeddingRequest.Model` is null).
    *   `CohereServiceCollectionExtensions.cs`: DI registration with `AddCohereEmbeddingClient()` and `AddCohereChatClient()` methods. Each method owns its options via closure (not registered in DI), enabling independent configuration. Keyed overloads (`AddCohereEmbeddingClient(string key, ...)`) support .NET 8 keyed services for registering multiple clients of the same interface.
    *   `ImageDataUriHelper.cs`: **Removed** — moved to `src/Cisharpai/` core as public utility (`ImageDataUriHelper.cs`).
    *   `Models/CohereStreamEvent.cs`: SSE event DTOs for streaming: `CohereStreamEvent`, `CohereStreamStartData`, `CohereContentDeltaData`, `CohereMessageEndData`, `CohereMessageEndUsage`.
    *   `Models/CohereChatRequest.cs`: Request DTOs for Cohere v2 chat API: `CohereChatMessage` (with optional ToolCallId, ToolCalls), `CohereChatRequest` (with `Documents`, `CitationOptions` for RAG, `Tools`, `ToolChoice`, `StrictTools` for tool calling, `Stream` bool? for streaming).
    *   `Models/CohereChatResponse.cs`: Response DTOs: `CohereChatContentBlock`, `CohereChatResponseMessage` (with optional `Citations` and `ToolCalls`), `CohereChatTokens`, `CohereChatBilledUnits`, `CohereChatUsage`, `CohereChatResponse`.
    *   `Models/CohereToolDefinition.cs`: DTOs for tool calling: `CohereToolDefinition`, `CohereToolFunction`, `CohereToolCallFunction`, `CohereToolCall`.
    *   `Models/CohereChatResponseFormat.cs`: DTO for `response_format` parameter with `json_object` type and optional `json_schema`.
    *   `Models/CohereChatDocument.cs`: DTO for `documents` array entries: `CohereChatDocument` (Id, Data as JsonElement) and `CohereCitationOptions` (Mode string).
    *   `Models/CohereChatCitation.cs`: Response DTOs for citations: `CohereChatCitation` (Start, End, Text, Sources, Type) and `CohereChatCitationSource` (Type, Id, Document dict).
    *   `Models/CohereEmbedInput.cs`: DTOs for Embed v4 `inputs` parameter: `CohereEmbedInput`, `CohereEmbedContentPart`, `CohereImageUrl`.
    *   `Models/CohereEmbedRequest.cs`: Request DTO with `Texts`, `Images`, `Inputs` (v4, mutually exclusive), `InputType`, `EmbeddingTypes`, `OutputDimension` (v4 Matryoshka).
    *   `Models/CohereEmbedResponse.cs`: Response DTO with `CohereEmbeddings`, `CohereBilledUnits` (includes `ImageTokens` for v4), `CohereImageMetadata`.

### Testing Package (`src/Cisharpai.Testing/`)
Lightweight fake clients for unit testing application code that depends on Cisharpai interfaces. No real HTTP calls are made.

*   **`FakeChatCompletionClient.cs`**: Fake implementation of `IChatCompletionClient`, `IStreamingChatFeature`, `IToolCallingFeature`, `IJsonOutputFeature`, `IGroundedChatFeature`. Supports response queues, defaults, and request capture.
*   **`FakeEmbeddingClient.cs`**: Fake implementation of `IEmbeddingClient`, `IImageEmbeddingFeature`, `IMultimodalEmbeddingFeature`. Supports response queues, defaults, and request capture.
*   **`FakeResponses.cs`**: Static factory methods for creating common fake responses (`Chat`, `ChatError`, `ToolCall`, `ToolCalls`, `GroundedChat`, `StreamingChunks`, `Embedding`, `Embeddings`, `EmbeddingError`).
*   **`FakeChatFeatures.cs`**: `[Flags]` enum controlling which features are registered on the fake chat client.
*   **`FakeEmbeddingFeatures.cs`**: `[Flags]` enum controlling which features are registered on the fake embedding client.
*   **`FakeServiceCollectionExtensions.cs`**: DI helpers (`AddFakeChatCompletionClient`, `AddFakeEmbeddingClient`) that register fakes and return the instance for setup/assertions.

### Console App (`src/Cisharp.Console/`)
Interactive demo application showcasing all provider integrations through a scenario-based menu.

*   **`Program.cs`**: Entry point with Spectre.Console-powered scenario selection menu.
*   **`Configuration/DotEnv.cs`**: Loads API keys from `.env` files.
*   **`Scenarios/`**: Each scenario demonstrates a specific provider/capability:
    *   `IScenario.cs`: Interface for runnable scenarios.
    *   `ScenarioRegistry.cs`: Dynamic discovery and registration of all scenarios.
    *   `ScenarioHelpers.cs`: Shared helpers for scenario output formatting.
    *   `OpenAiChatScenario.cs`: OpenAI chat completion demo.
    *   `OpenAiEmbeddingScenario.cs`: OpenAI embedding demo.
    *   `AnthropicChatScenario.cs`: Anthropic (Claude) chat completion demo.
    *   `AzureOpenAiChatScenario.cs`: Azure OpenAI chat completion demo.
    *   `AzureAiInferenceChatScenario.cs`: Azure AI Inference chat completion demo.
    *   `CohereEmbeddingScenario.cs`: Cohere embedding demo.
    *   `CohereChatScenario.cs`: Cohere chat completion demo.
    *   `CohereGroundedChatScenario.cs`: Cohere grounded chat (RAG) with documents and citations demo.
    *   `OpenAiJsonOutputScenario.cs`: OpenAI JSON Mode and Structured Outputs demo.
    *   `ToolCallingScenario.cs`: Multi-provider tool calling demo (OpenAI, Anthropic, Cohere) with full tool-call loop and mock weather data.
    *   `OpenAiVisionScenario.cs`: OpenAI vision demo — prompts for local image file path, sends to gpt-4o via `LlmMessage.WithImage`.
    *   `OpenAiStreamingScenario.cs`: OpenAI streaming demo — streams tokens via `IStreamingChatFeature` with typewriter effect.

### Wiki (`wiki/`)
Project documentation pages.

*   `index.md`: Main landing page.
*   `getting-started.md`: Setup and basic usage guide.
*   `openai.md`: OpenAI-specific quickstart.
*   `embeddings.md`: Comprehensive embeddings guide across providers.
*   `feature-extensions.md`: Feature Collection Pattern documentation.
*   `json-output.md`: JSON Mode and Structured Outputs documentation (quick start, provider support matrix, schema guidelines, refusal handling, troubleshooting).
*   `grounded-chat.md`: Grounded Chat (RAG) documentation (quick start, document formats, citation modes, working with citations, provider support).
*   `provider-features.md`: Provider Feature Matrix — lists every feature (chat, embeddings, JSON output, image embeddings, multimodal embeddings, grounded chat, tool calling, vision, streaming) supported by each provider. Must be updated when features are added or removed.
*   `tool-calling.md`: Tool Calling (Function Calling) documentation (quick start, multi-turn conversation, ToolChoice options, core models, provider differences, strict mode, error handling).
*   `vision.md`: Vision documentation (quick start, MessageContentPart types, factory methods, provider support matrix, Anthropic raw base64 note, Cohere skip behavior).
*   `streaming.md`: Streaming documentation (quick start, ChatCompletionChunk model, content accumulation, cancellation, provider-specific SSE formats, resilience handler for long streams).

### CI/CD & Build

#### GitHub Actions Workflows (`.github/workflows/`)
*   **`ci.yml`**: Main CI workflow. Triggers on push/PR/workflow_dispatch. Sets up .NET 8 & 10, runs `scripts/build.ps1` for build+unit tests, then runs integration tests with API key secrets. Publishes test reports and uploads NuGet artifacts.
*   **`pipeline.yml`**: Alternative pipeline with manual GitVersion invocation, explicit versioning in build/pack steps, and tag-triggered NuGet publish to nuget.org.
*   **`codeql.yml`**: CodeQL security analysis. Runs on push/PR and weekly schedule. Performs autobuild-based C# code scanning.

#### Build Configuration
*   **`GitVersion.yml`**: Configures GitVersion in `ContinuousDeployment` mode. Branch labels: `alpha` for develop/feature, `beta` for release/hotfix, none for main.
*   **`.config/dotnet-tools.json`**: Local dotnet tool manifest. Includes `gitversion.tool` v6.4.0.
*   **`scripts/build.ps1`**: PowerShell build script (modeled after NStore). Parameters: `$nugetApiKey`, `$nugetPublish`, `-skiptest`. Steps: tool restore, GitVersion, restore, build (Release), test (unit tests on net8.0+net10.0), pack (5 library projects), optional publish to nuget.org. Outputs to `artifacts/NuGet/` (packages) and `artifacts/TestResults/` (trx files).
*   **`scripts/gh-secrets-from-dotenv.zsh`**: Zsh utility that reads `.env` and sets GitHub Actions/Codespaces secrets via `gh` CLI. Validates against an allowlist of known environment variable names.
*   **`Directory.Build.props`**: Global MSBuild properties for packaging (authors, license, SourceLink, symbols).
*   **`Directory.Packages.props`**: Central package version management.
*   **`.envsample`**: Template `.env` file listing all required environment variables with placeholder values for all providers.

### Developer Agent Customizations
Project-scoped Claude agents live in `.claude/agents/`.

*   **`pr-check-fixer.md`**: Custom PR remediation agent that works only on the pull request associated with the current branch. It uses the `pr-expert` skill to iteratively diagnose failing checks, implement fixes, run validation, commit and push changes, and watch remote PR checks until all fixable blockers are resolved.

### Testing
*   **`src/Cisharpai.Tests.Common/`**: Shared test utilities referenced by all test projects.
    *   `DotEnvLoader.cs`: Static utility class to load environment variables from a `.env` file. Searches current and parent directories.
    *   `TestEnvironmentVariables.cs`: Constants for environment variable names used in integration tests.
*   **`src/Cisharpai.Tests/`**: Unit tests.
    *   `Features/FeatureCollectionTests.cs`: Tests for `FeatureCollection` (Get/Set/enumeration/thread-safety).
    *   `Features/FeatureDiscoveryTests.cs`: Tests verifying feature discovery across all client implementations (including IJsonOutputFeature, IGroundedChatFeature on Cohere, IToolCallingFeature on OpenAI/Azure OpenAI/Azure AI Inference/Anthropic/Cohere, IStreamingChatFeature on all 5 chat clients).
    *   `DependencyInjection/CohereDiRegistrationTests.cs`: Tests that both Cohere chat and embedding clients resolve correctly with independent options.
    *   `DependencyInjection/OpenAiDiRegistrationTests.cs`: Tests that both OpenAI chat and embedding clients resolve correctly with independent options.
    *   `DependencyInjection/AzureOpenAiDiRegistrationTests.cs`: Tests that both Azure OpenAI chat and embedding clients resolve correctly with independent options.
    *   `DependencyInjection/AzureAiInferenceDiRegistrationTests.cs`: Tests that both Azure AI Inference chat and embedding clients resolve correctly with independent options.
    *   `DependencyInjection/AnthropicDiRegistrationTests.cs`: Tests that Anthropic chat client resolves correctly.
    *   `Cohere/CohereImageEmbeddingTests.cs`: Tests for Cohere image embedding request/response mapping and data URI format.
    *   `Cohere/CohereMultimodalEmbeddingTests.cs`: Tests for Cohere Embed v4 multimodal embedding (text-only, image-only, mixed, batch, output_dimension, input types, raw response, error handling, image tokens).
    *   `Cohere/CohereChatCompletionTests.cs`: Tests for Cohere chat completion request/response mapping (messages, roles, snake_case naming, tokens, raw response, error handling).
    *   `Cohere/CohereJsonOutputTests.cs`: Tests for Cohere JSON output request building (JSON Mode response_format, system message injection, Structured Outputs with json_schema, markdown fence stripping, feature discovery).
    *   `Cohere/CohereGroundedChatTests.cs`: Tests for Cohere grounded chat request building (documents array, key-value/plain-text formats, citation_options modes, snake_case naming, no response_format), response mapping (citations, sources, content, tokens), error handling, and feature discovery.
    *   `Cohere/ImageDataUriHelperTests.cs`: Tests for data URI helper (MIME type mapping for PNG/JPEG/WebP/GIF, base64 encoding).
    *   `Azure/AzureAiInference/AzureAiInferenceImageEmbeddingTests.cs`: Tests for Azure AI Inference image embedding.
    *   `Models/JsonOutputOptionsTests.cs`: Tests for `JsonOutputOptions` validation (JsonMode valid with/without schema, JsonSchema validation scenarios, Strict defaults).
    *   `Models/GroundedChatOptionsTests.cs`: Tests for `GroundedChatOptions` validation (with/without documents, null documents, default CitationMode).
    *   `Models/DocumentChunkTests.cs`: Tests for `DocumentChunk` validation (Data-only, Text-only, both, neither, empty).
    *   `OpenAi/OpenAiJsonOutputTests.cs`: Tests for OpenAI JSON output request building (JSON Mode for legacy/reasoning/GPT-5, Structured Outputs, refusal handling, system message injection, feature discovery).
    *   `OpenAi/OpenAiDefaultModelTests.cs`: Tests for OpenAI default model resolution (fallback to DefaultModel, request override, no-model exception, model constants validation).
    *   `Anthropic/AnthropicDefaultModelTests.cs`: Tests for Anthropic default model resolution (fallback to DefaultModel, request override, no-model exception, model constants validation).
    *   `Cohere/CohereDefaultModelTests.cs`: Tests for Cohere default model resolution (chat and embedding clients, fallback to DefaultModel, request override, no-model exception, model constants validation).
    *   `Azure/Common/`: Tests for shared Azure authentication handler and client options.
    *   `Azure/AzureOpenAi/`: Tests for Azure OpenAI client.
    *   `Azure/AzureOpenAi/AzureOpenAiDefaultModelTests.cs`: Tests for Azure OpenAI default model resolution (fallback to DefaultModel for reasoning detection, request override, null model treated as legacy, JSON output with DefaultModel).
    *   `Azure/AzureOpenAi/AzureOpenAiJsonOutputTests.cs`: Tests for Azure OpenAI JSON output request building (JSON Mode, Structured Outputs, refusal, feature discovery, endpoint/header verification).
    *   `Azure/AzureOpenAi/AzureOpenAiToolCallingTests.cs`: Tests for Azure OpenAI tool calling request serialization (tools array, all ToolChoice variants, multi-turn), response deserialization (single/multiple tool calls, text response), error handling, feature discovery, strict flag, endpoint verification.
    *   `Azure/AzureAiInference/`: Tests for Azure AI Inference client.
    *   `Azure/AzureAiInference/AzureAiInferenceJsonOutputTests.cs`: Tests for Azure AI Inference JSON output request building (JSON Mode, Structured Outputs, schema parsing, feature discovery, endpoint/model verification).
    *   `Azure/AzureAiInference/AzureAiInferenceToolCallingTests.cs`: Tests for Azure AI Inference tool calling request serialization (tools array, all ToolChoice variants, multi-turn), response deserialization (single/multiple tool calls, text response), error handling, feature discovery, strict flag, endpoint/model verification.
    *   `Anthropic/AnthropicJsonOutputTests.cs`: Tests for Anthropic JSON output request building (JSON Mode system message injection, Structured Outputs via output_config, schema parsing, refusal handling, feature discovery).
    *   `Models/ToolDefinitionTests.cs`: Tests for `ToolDefinition` validation (name required, parameters must be JSON object).
    *   `Models/ToolCallTests.cs`: Tests for `ToolCall` properties and immutability.
    *   `Models/ToolResultTests.cs`: Tests for `ToolResult` properties and IsError default.
    *   `Models/ToolChoiceTests.cs`: Tests for `ToolChoice` variants (Auto/None/Required/Specific), singletons, equality.
    *   `Models/LlmMessageToolTests.cs`: Tests for LlmMessage with Tool role, backward compatibility, ToolCalls and ToolCallId properties.
    *   `Models/ToolCallingOptionsTests.cs`: Tests for `ToolCallingOptions` validation (empty tools, cascading validation).
    *   `Models/ToolCallingResponseTests.cs`: Tests for `ToolCallingResponse` convenience properties and Error factory.
    *   `OpenAi/OpenAiToolCallingTests.cs`: Tests for OpenAI tool calling request serialization (tools array, all ToolChoice variants, multi-turn), response deserialization (single/multiple tool calls, text response), error handling, feature discovery, strict flag.
    *   `Anthropic/AnthropicToolCallingTests.cs`: Tests for Anthropic tool calling (input_schema, ToolChoice mapping, tool_use/tool_result content blocks, multi-turn, error handling, feature discovery).
    *   `Cohere/CohereToolCallingTests.cs`: Tests for Cohere tool calling (snake_case, uppercase ToolChoice, strict_tools flag, Specific degradation, multi-turn, error handling, feature discovery).
    *   `OpenAi/OpenAiVisionTests.cs`: Tests for OpenAI vision (base64 data URI format, WithImage file loading, TextContentPart arrays, normal string content, WithBase64Image media type).
    *   `OpenAi/OpenAiStreamingTests.cs`: Tests for OpenAI streaming (legacy SSE chunks, finish reason, token usage, stream:true in request, Responses API gpt-5 events, feature discovery, model on chunks).
    *   `Azure/AzureOpenAi/AzureOpenAiVisionTests.cs`: Tests for Azure OpenAI vision (base64 data URI, WithBase64Image, normal string, file path loading).
    *   `Azure/AzureOpenAi/AzureOpenAiStreamingTests.cs`: Tests for Azure OpenAI streaming (text chunks, finish reason, token usage, stream:true, feature discovery, reasoning model uses max_completion_tokens).
    *   `Azure/AzureAiInference/AzureAiInferenceVisionTests.cs`: Tests for Azure AI Inference vision (same format as Azure OpenAI).
    *   `Azure/AzureAiInference/AzureAiInferenceStreamingTests.cs`: Tests for Azure AI Inference streaming (text chunks, finish reason, feature discovery, model from options when request.Model is null).
    *   `Anthropic/AnthropicVisionTests.cs`: Tests for Anthropic vision (raw base64 NOT data URIs, source format, file path loading with actual bytes, normal string, structure verification).
    *   `Anthropic/AnthropicStreamingTests.cs`: Tests for Anthropic streaming (event-based SSE, message_start/content_block_delta/message_delta events, no [DONE] sentinel, model from message_start, feature discovery).
    *   `Cohere/CohereVisionTests.cs`: Tests for Cohere vision skip behavior (mixed text+image, image-only, base64, multiple text parts, normal message, WithImage factory).
    *   `Cohere/CohereStreamingTests.cs`: Tests for Cohere streaming (content-delta/message-end events, text chunks in order, finish reason, token counts, stream:true in request, feature discovery).
    *   `Core/HttpClientBuilderExtensionsTests.cs`: Tests for `AddCisharpaiResilienceHandler()` and `AddCisharpaiStreamingResilienceHandler()` extension methods.
    *   `Testing/FakeChatCompletionClientTests.cs`: Tests for `FakeChatCompletionClient` (queued/default responses, request capture, feature opt-out, reset, streaming, tool calling, JSON output, grounded chat).
    *   `Testing/FakeEmbeddingClientTests.cs`: Tests for `FakeEmbeddingClient` (queued/default responses, request capture, feature opt-out, image/multimodal embedding).
    *   `Testing/FakeResponsesTests.cs`: Tests for `FakeResponses` static factories (all response types, custom parameters, error responses).
    *   `Testing/FakeServiceCollectionExtensionsTests.cs`: Tests for DI registration helpers (resolution, feature discovery, selective features).
*   **`src/Cisharpai.Integration.Tests/`**: Integration tests verifying connection to real APIs.
    *   `EnvironmentConfigurationTests.cs`: Single test that validates all required environment variables for all providers. If any are missing, it fails with a clear error message showing which variables are missing and provides example `.env` file content to fix it.
    *   `DotEnv.cs`: Helper class that delegates to `DotEnvLoader` and re-exports `TestEnvironmentVariables` constants for backwards compatibility.
    *   `OpenAi/OpenAiChatCompletionIntegrationTests.cs`: Tests OpenAI models (gpt-4.1-nano, gpt-5-nano).
    *   `OpenAi/OpenAiEmbeddingIntegrationTests.cs`: Tests OpenAI embedding models.
    *   `OpenAi/OpenAiJsonOutputIntegrationTests.cs`: Integration tests for OpenAI JSON Mode (gpt-4.1-nano, gpt-5-nano) and Structured Outputs (simple/complex schemas, feature discovery).
    *   `Anthropic/AnthropicChatCompletionIntegrationTests.cs`: Tests Anthropic models (claude-opus-4-5, claude-sonnet-4-5, claude-haiku-4-5).
    *   `Anthropic/AnthropicJsonOutputIntegrationTests.cs`: Integration tests for Anthropic JSON Mode and Structured Outputs (simple/complex schemas, feature discovery).
    *   `AzureOpenAi/AzureOpenAiChatCompletionIntegrationTests.cs`: Tests Azure OpenAI deployments (from `AZURE_OPENAI_TEST_DEPLOYMENTS` env var, comma-separated).
    *   `AzureOpenAi/AzureOpenAiEmbeddingIntegrationTests.cs`: Tests Azure OpenAI embedding deployments (from `AZURE_OPENAI_TEST_EMBEDDING_DEPLOYMENT` env var).
    *   `AzureOpenAi/AzureOpenAiJsonOutputIntegrationTests.cs`: Integration tests for Azure OpenAI JSON Mode and Structured Outputs (graceful handling for unsupported deployments, feature discovery).
    *   `AzureOpenAi/AzureOpenAiToolCallingIntegrationTests.cs`: Integration tests for Azure OpenAI tool calling (single tool call, ToolChoice.Required, multi-turn loop, feature discovery).
    *   `AzureAiInference/AzureAiInferenceChatCompletionIntegrationTests.cs`: Tests Azure AI Inference models (from `AZURE_INFERENCE_TEST_MODELS` env var, comma-separated).
    *   `AzureAiInference/AzureAiInferenceJsonOutputIntegrationTests.cs`: Integration tests for Azure AI Inference JSON Mode and Structured Outputs (graceful handling for unsupported models, feature discovery).
    *   `AzureAiInference/AzureAiInferenceToolCallingIntegrationTests.cs`: Integration tests for Azure AI Inference tool calling (single tool call, ToolChoice.Required, multi-turn loop, feature discovery). Uses `Assert.Inconclusive` for unsupported models and `OneTimeTearDown` to verify at least one model passed strict assertions.
    *   `AzureAiInference/AzureAiInferenceEmbeddingIntegrationTests.cs`: Integration tests for Azure AI Inference text and image embeddings (basic text, batch, dimension control, image via IImageEmbeddingFeature, feature discovery). Uses dedicated env vars `AZURE_INFERENCE_TEST_EMBEDDING_ENDPOINT`, `AZURE_INFERENCE_TEST_EMBEDDING_KEY`, `AZURE_INFERENCE_TEST_EMBEDDING_MODEL`.
    *   `Cohere/CohereEmbeddingIntegrationTests.cs`: Integration tests for Cohere text embedding.
    *   `Cohere/CohereImageEmbeddingIntegrationTests.cs`: Integration tests for Cohere image embedding via feature discovery.
    *   `Cohere/CohereMultimodalEmbeddingIntegrationTests.cs`: Integration tests for Cohere Embed v4 multimodal embedding (text-only, image-only, mixed text+image, output dimension control, batch inputs, feature discovery).
    *   `Cohere/CohereChatCompletionIntegrationTests.cs`: Integration tests for Cohere chat completion (command-a-03-2025, command-r-plus-08-2024).
    *   `Cohere/CohereJsonOutputIntegrationTests.cs`: Integration tests for Cohere JSON Mode and Structured Outputs (simple/complex schemas, feature discovery).
    *   `Cohere/CohereGroundedChatIntegrationTests.cs`: Integration tests for Cohere grounded chat with key-value and plain-text documents, citation offset verification, fast mode, feature discovery.
    *   `OpenAi/OpenAiToolCallingIntegrationTests.cs`: Integration tests for OpenAI tool calling (single tool call, ToolChoice.Required, ToolChoice.None, multi-turn loop, feature discovery).
    *   `Anthropic/AnthropicToolCallingIntegrationTests.cs`: Integration tests for Anthropic tool calling (single tool call, ToolChoice.Required, multi-turn loop, feature discovery).
    *   `Cohere/CohereToolCallingIntegrationTests.cs`: Integration tests for Cohere tool calling (single tool call, ToolChoice.Required, multi-turn loop, feature discovery).
    *   `OpenAi/OpenAiVisionIntegrationTests.cs`: Integration tests for OpenAI vision with gpt-4.1-nano (base64 image input, file path via LlmMessage.WithImage).
    *   `Anthropic/AnthropicVisionIntegrationTests.cs`: Integration tests for Anthropic vision with claude-haiku-4-5-20251001 (base64 image, file path).
    *   `OpenAi/OpenAiStreamingIntegrationTests.cs`: Integration tests for OpenAI streaming with gpt-4.1-nano (feature discovery, basic stream, finish reason, content accumulation).
    *   `Anthropic/AnthropicStreamingIntegrationTests.cs`: Integration tests for Anthropic streaming with claude-haiku-4-5-20251001.
    *   `Cohere/CohereStreamingIntegrationTests.cs`: Integration tests for Cohere streaming with command-a-03-2025.

# Integration tests

Important: Integration tests has a separate project `src/Cisharpai.Integration.Tests/DotEnv.cs` that is compiled only for .NET 10 to limit the number of call to real provdier.

**Maintenance Note:** When adding or modifying environment variables for integration tests, update the following files to keep them in sync:
1. `src/Cisharpai.Tests.Common/TestEnvironmentVariables.cs` - Add the constant for the new variable
2. `src/Cisharpai.Integration.Tests/DotEnv.cs` - Re-export the constant from `TestEnvironmentVariables` for backwards compatibility
3. `src/Cisharpai.Integration.Tests/EnvironmentConfigurationTests.cs` - Add to the validation array
4. `scripts/gh-secrets-from-dotenv.zsh` - Add to the `allowlist` array (for GitHub Actions/Codespaces secrets)
5. `memories/project_overview.md` - Update the `.env` example above

**Maintenance Note:** When adding or removing a feature on any provider (e.g., implementing a new feature interface like `IJsonOutputFeature`, `IImageEmbeddingFeature`, `IMultimodalEmbeddingFeature`, or adding a new provider), update these files:
1. `wiki/provider-features.md` - Update the support matrix table and the provider details section
2. `memories/project_overview.md` - Update the project structure to reflect the new capability
