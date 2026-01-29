# Project Overview

## Description
**Cisharpai** is a unified .NET client library designed to provide a common interface for interacting with various Large Language Model (LLM) providers. It abstracts the differences between provider APIs (OpenAI, Azure OpenAI, Anthropic), allowing developers to switch providers with minimal code changes.

## Project Structure

### Core Library (`src/Cisharpai/`)
Contains the abstractions and shared logic. This is the only dependency needed for the consuming application logic.

*   **`IChatCompletionClient.cs`**: The primary interface for performing chat completion requests.
*   **`Models/`**:
    *   **`ChatCompletionRequest.cs`**: Unified request model (Messages, Model, Temperature, etc.).
    *   **`ChatCompletionResponse.cs`**: Unified response model (Content, Usage stats, optional Status/IncompleteReason for Responses API, IsSuccess/ErrorMessage for error handling). Provider clients never throw exceptions; errors are returned via `IsSuccess = false` and `ErrorMessage`. Includes a static `Error()` factory method.
    *   **`LlmMessage.cs`**: Represents a message in the conversation (Role, Content).
*   **`LlmHttpClient.cs`**: Internal helper for handling HTTP requests to the providers.

### Provider Implementations
Each supported provider has its own project providing concrete implementations of the core interfaces.

*   **`src/Cisharpai.OpenAi/`**: Connector for standard OpenAI API. Supports legacy Chat Completions API (GPT-4, etc.), reasoning models (o1/o3/o4), and the Responses API (GPT-5) with status/incomplete handling.
    *   `OpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient`. Routes to the correct endpoint/format based on model detection.
*   **`src/Cisharpai.AzureOpenAi/`**: Connector for Azure OpenAI Service. Supports both legacy models and reasoning/GPT-5 models (uses `max_completion_tokens` instead of `max_tokens`). Supports API key and Azure AD (TokenCredential) authentication.
    *   `AzureOpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient` with Azure-specific auth/routing. Detects reasoning models (o1/o3/o4/gpt-5) and uses appropriate request format.
*   **`src/Cisharpai.Anthropic/`**: Connector for Anthropic (Claude) API.
    *   `AnthropicChatCompletionClient.cs`: Implements `IChatCompletionClient`.

### Testing
*   **`src/Cisharpai.Tests/`**: Unit tests.
*   **`src/Cisharpai.Integration.Tests/`**: Integration tests verifying connection to real APIs.
    *   `OpenAi/OpenAiChatCompletionIntegrationTests.cs`: Tests OpenAI models (gpt-4.1-nano, gpt-5-nano).
    *   `Anthropic/AnthropicChatCompletionIntegrationTests.cs`: Tests Anthropic models (claude-opus-4-5, claude-sonnet-4-5, claude-haiku-4-5).
    *   `AzureOpenAi/AzureOpenAiChatCompletionIntegrationTests.cs`: Tests Azure OpenAI deployments (from `AZURE_OPENAI_TEST_DEPLOYMENTS` env var, comma-separated).
