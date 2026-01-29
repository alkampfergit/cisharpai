# Project Overview

## Description
**Cisharpai** is a unified .NET client library designed to provide a common interface for interacting with various Large Language Model (LLM) providers. It abstracts the differences between provider APIs (OpenAI, Azure OpenAI, Anthropic), allowing developers to switch providers with minimal code changes.

## Project Structure

### Core Library (`src/Cisharpai/`)
Contains the abstractions and shared logic. This is the only dependency needed for the consuming application logic.

*   **`IChatCompletionClient.cs`**: The primary interface for performing chat completion requests.
*   **`Models/`**:
    *   **`ChatCompletionRequest.cs`**: Unified request model (Messages, Model, Temperature, etc.).
    *   **`ChatCompletionResponse.cs`**: Unified response model (Content, Usage stats).
    *   **`LlmMessage.cs`**: Represents a message in the conversation (Role, Content).
*   **`LlmHttpClient.cs`**: Internal helper for handling HTTP requests to the providers.

### Provider Implementations
Each supported provider has its own project providing concrete implementations of the core interfaces.

*   **`src/Cisharpai.OpenAi/`**: Connector for standard OpenAI API.
    *   `OpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient`.
*   **`src/Cisharpai.AzureOpenAi/`**: Connector for Azure OpenAI Service.
    *   `AzureOpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient` with Azure-specific auth/routing.
*   **`src/Cisharpai.Anthropic/`**: Connector for Anthropic (Claude) API.
    *   `AnthropicChatCompletionClient.cs`: Implements `IChatCompletionClient`.

### Testing
*   **`src/Cisharpai.Tests/`**: Unit tests.
*   **`src/Cisharpai.Integration.Tests/`**: Integration tests verifying connection to real APIs.
