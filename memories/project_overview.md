# Project Overview

## Description
**Cisharpai** is a unified .NET client library designed to provide a common interface for interacting with various Large Language Model (LLM) providers. It abstracts the differences between provider APIs (OpenAI, Azure OpenAI, Anthropic), allowing developers to switch providers with minimal code changes.

## Foundation Principles & Design Guidelines

The design of Cisharpai is built on several key architectural decisions intended to balance simplicity with power:

1.  **Unified Abstraction**: The core goal is to allow applications to depend solely on `Cisharpai` (the core library). Provider-specific logic is encapsulated in separate packages, ensuring that switching from OpenAI to Anthropic is largely a configuration change.
2.  **"Escape Hatch" Extensibility**: We recognize that AI providers move faster than strongly-typed libraries can update. The `ExtraParameters` property (and the "Deep Merge" strategy) allows users to inject arbitrary JSON properties into requests. This ensures developers can use bleeding-edge features (like new sampling parameters) immediately without waiting for a library release.
3.  **No Exceptions for API Errors**: Clients do not throw exceptions for API failures (4xx/5xx). Instead, they return a response object where `IsSuccess` is false and `ErrorMessage` contains details. This promotes robust error handling flows. Exceptions are reserved for network transport issues or gross misuse (like missing configurations).
4.  **Debuggability First**: AI interactions can be opaque. All response objects allow access to `RawResponseJson` and `RawRequestJson` (when requested), enabling developers to see exactly what was sent and received over the wire.
5.  **Immutability**: Data transfer objects (Requests/Responses) are immutable `records`, promoting thread safety and predictable state management.

## Project Structure

### Core Library (`src/Cisharpai/`)
Contains the abstractions and shared logic. This is the only dependency needed for the consuming application logic.

*   **`IChatCompletionClient.cs`**: The primary interface for performing chat completion requests.
*   **`IEmbeddingClient.cs`**: The primary interface for generating text embeddings.
*   **`Models/`**:
    *   **`ChatCompletionRequest.cs`**: Unified request model (Messages, Model, Temperature, MaxTokens, IncludeRawResponse, ExtraParameters). The `ExtraParameters` property (`JsonElement?`) allows passing arbitrary JSON that is deeply merged into the provider-specific request body, enabling use of new model features without DTO changes.
    *   **`ChatCompletionResponse.cs`**: Unified response model (Content, Usage stats, optional Status/IncompleteReason for Responses API, IsSuccess/ErrorMessage for error handling, RawResponseJson/RawRequestJson for debug inspection). Provider clients never throw exceptions; errors are returned via `IsSuccess = false` and `ErrorMessage`. Includes a static `Error()` factory method.
    *   **`EmbeddingRequest.cs`**: Unified request model for embedding operations (Input, Model, InputType, Dimensions, EncodingFormat, ExtraParameters).
    *   **`EmbeddingResponse.cs`**: Unified response model for embeddings (Embeddings, Base64Embeddings, Model, TotalTokens, Dimensions, RawResponseJson, RawRequestJson, IsSuccess/ErrorMessage).
    *   **`LlmMessage.cs`**: Represents a message in the conversation (Role, Content).
*   **`JsonDeepMerge.cs`**: Static utility for deeply merging a JSON override document into a base JSON document. Objects are merged recursively; arrays and scalars are replaced by overrides.
*   **`LlmHttpClient.cs`**: Internal helper for handling HTTP requests to the providers. Supports optional `extraParameters` (`JsonElement?`) that are deeply merged into the serialized request payload before sending. `PostWithRawAsync` returns both raw response JSON and raw request JSON for debug inspection.

### Provider Implementations
Each supported provider has its own project providing concrete implementations of the core interfaces.

*   **`src/Cisharpai.OpenAi/`**: Connector for standard OpenAI API. Supports legacy Chat Completions API (GPT-4, etc.), reasoning models (o1/o3/o4), and the Responses API (GPT-5) with status/incomplete handling.
    *   `OpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient`. Routes to the correct endpoint/format based on model detection.
    *   `OpenAiEmbeddingClient.cs`: Implements `IEmbeddingClient`.
*   **`src/Cisharpai.AzureOpenAi/`**: Connector for Azure OpenAI Service. Supports both legacy models and reasoning/GPT-5 models (uses `max_completion_tokens` instead of `max_tokens`). Supports API key and Azure AD (TokenCredential) authentication.
    *   `AzureOpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient` with Azure-specific auth/routing. Detects reasoning models (o1/o3/o4/gpt-5) and uses appropriate request format.
*   **`src/Cisharpai.Anthropic/`**: Connector for Anthropic (Claude) API.
    *   `AnthropicChatCompletionClient.cs`: Implements `IChatCompletionClient`.
*   **`src/Cisharpai.Cohere/`**: Connector for Cohere API.
    *   `CohereEmbeddingClient.cs`: Implements `IEmbeddingClient`. Supports input types (search_query, search_document, classification, clustering).

### Testing
*   **`src/Cisharpai.Tests/`**: Unit tests.
*   **`src/Cisharpai.Integration.Tests/`**: Integration tests verifying connection to real APIs.
    *   `OpenAi/OpenAiChatCompletionIntegrationTests.cs`: Tests OpenAI models (gpt-4.1-nano, gpt-5-nano).
    *   `Anthropic/AnthropicChatCompletionIntegrationTests.cs`: Tests Anthropic models (claude-opus-4-5, claude-sonnet-4-5, claude-haiku-4-5).
    *   `AzureOpenAi/AzureOpenAiChatCompletionIntegrationTests.cs`: Tests Azure OpenAI deployments (from `AZURE_OPENAI_TEST_DEPLOYMENTS` env var, comma-separated).
