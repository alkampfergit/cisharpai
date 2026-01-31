# Project Overview

## Description
**Cisharpai** is a unified .NET client library designed to provide a common interface for interacting with various Large Language Model (LLM) providers. It abstracts the differences between provider APIs (OpenAI, Azure OpenAI, Anthropic, Cohere), allowing developers to switch providers with minimal code changes. Supports both chat completions and text embeddings.

## Project Structure

### Core Library (`src/Cisharpai/`)
Contains the abstractions and shared logic. This is the only dependency needed for the consuming application logic.

*   **`IChatCompletionClient.cs`**: The primary interface for performing chat completion requests.
*   **`IEmbeddingClient.cs`**: Interface for performing embedding requests. Defines `GetEmbeddingsAsync(EmbeddingRequest, CancellationToken)`.
*   **`Models/`**:
    *   **`ChatCompletionRequest.cs`**: Unified request model (Messages, Model, Temperature, MaxTokens, IncludeRawResponse, ExtraParameters). The `ExtraParameters` property (`JsonElement?`) allows passing arbitrary JSON that is deeply merged into the provider-specific request body, enabling use of new model features without DTO changes.
    *   **`ChatCompletionResponse.cs`**: Unified response model (Content, Usage stats, optional Status/IncompleteReason for Responses API, IsSuccess/ErrorMessage for error handling, RawResponseJson/RawRequestJson for debug inspection). Provider clients never throw exceptions; errors are returned via `IsSuccess = false` and `ErrorMessage`. Includes a static `Error()` factory method.
    *   **`EmbeddingRequest.cs`**: Unified embedding request model (Input, Model, InputType, Dimensions, EncodingFormat, IncludeRawResponse, ExtraParameters).
    *   **`EmbeddingResponse.cs`**: Unified embedding response model (Embeddings float arrays, Base64Embeddings, Model, TotalTokens, Dimensions, RawResponseJson/RawRequestJson, IsSuccess/ErrorMessage). Includes static `Error()` factory method.
    *   **`EmbeddingInputType.cs`**: Enum for embedding input type hints (Query, Document, Classification, Clustering).
    *   **`LlmMessage.cs`**: Represents a message in the conversation (Role, Content).
*   **`JsonDeepMerge.cs`**: Static utility for deeply merging a JSON override document into a base JSON document. Objects are merged recursively; arrays and scalars are replaced by overrides.
*   **`LlmHttpClient.cs`**: Internal helper for handling HTTP requests to the providers. Supports optional `extraParameters` (`JsonElement?`) that are deeply merged into the serialized request payload before sending. `PostWithRawAsync` returns both raw response JSON and raw request JSON for debug inspection.

### Provider Implementations
Each supported provider has its own project providing concrete implementations of the core interfaces.

*   **`src/Cisharpai.OpenAi/`**: Connector for standard OpenAI API. Supports legacy Chat Completions API (GPT-4, etc.), reasoning models (o1/o3/o4), the Responses API (GPT-5) with status/incomplete handling, and text embeddings.
    *   `OpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient`. Routes to the correct endpoint/format based on model detection.
    *   `OpenAiEmbeddingClient.cs`: Implements `IEmbeddingClient`. Posts to `/embeddings` endpoint. Supports single/batch input, custom dimensions, and base64 encoding format.
    *   `OpenAiServiceCollectionExtensions.cs`: DI registration via `AddOpenAiClient()` for chat and `AddOpenAiEmbeddingClient()` for embeddings.
*   **`src/Cisharpai.AzureOpenAi/`**: Connector for Azure OpenAI Service. Supports both legacy models and reasoning/GPT-5 models (uses `max_completion_tokens` instead of `max_tokens`). Supports API key and Azure AD (TokenCredential) authentication.
    *   `AzureOpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient` with Azure-specific auth/routing. Detects reasoning models (o1/o3/o4/gpt-5) and uses appropriate request format.
*   **`src/Cisharpai.Anthropic/`**: Connector for Anthropic (Claude) API.
    *   `AnthropicChatCompletionClient.cs`: Implements `IChatCompletionClient`.
*   **`src/Cisharpai.Cohere/`**: Connector for Cohere API v2. Supports text embeddings.
    *   `CohereEmbeddingClient.cs`: Implements `IEmbeddingClient`. Posts to `/embed` endpoint. Maps `EmbeddingInputType` to Cohere's `input_type` (search_query, search_document, classification, clustering).
    *   `CohereServiceCollectionExtensions.cs`: DI registration via `AddCohereEmbeddingClient()`.

### Testing
*   **`src/Cisharpai.Tests/`**: Unit tests.
    *   `OpenAi/OpenAiChatCompletionClientTests.cs`: Chat completion unit tests.
    *   `OpenAi/OpenAiEmbeddingClientTests.cs`: Embedding unit tests (request mapping, response mapping, endpoint, single/batch, dimensions, raw JSON, errors).
    *   `Cohere/CohereEmbeddingClientTests.cs`: Cohere embedding unit tests (request mapping, response mapping, endpoint, input types, errors).
*   **`src/Cisharpai.Integration.Tests/`**: Integration tests verifying connection to real APIs.
    *   `OpenAi/OpenAiChatCompletionIntegrationTests.cs`: Tests OpenAI models (gpt-4.1-nano, gpt-5-nano).
    *   `OpenAi/OpenAiEmbeddingIntegrationTests.cs`: Tests OpenAI embedding models (text-embedding-3-small, text-embedding-3-large).
    *   `Anthropic/AnthropicChatCompletionIntegrationTests.cs`: Tests Anthropic models (claude-opus-4-5, claude-sonnet-4-5, claude-haiku-4-5).
    *   `AzureOpenAi/AzureOpenAiChatCompletionIntegrationTests.cs`: Tests Azure OpenAI deployments (from `AZURE_OPENAI_TEST_DEPLOYMENTS` env var, comma-separated).
    *   `Cohere/CohereEmbeddingIntegrationTests.cs`: Tests Cohere embedding models (embed-english-v3.0) with different input types.
