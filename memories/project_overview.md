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
    *   `Chat/IJsonOutputFeature.cs`: Optional feature for JSON output enforcement on chat completion clients via `GetChatCompletionWithJsonOutputAsync(request, jsonOutputOptions)`. Supports JSON Mode (`json_object`) and Structured Outputs (`json_schema`). Registered on OpenAI, Azure OpenAI, Azure AI Inference, and Anthropic clients.
*   **`Models/`**:
    *   **`ChatCompletionRequest.cs`**: Unified request model (Messages, Model, Temperature, MaxTokens, IncludeRawResponse, ExtraParameters). The `ExtraParameters` property (`JsonElement?`) allows passing arbitrary JSON that is deeply merged into the provider-specific request body, enabling use of new model features without DTO changes.
    *   **`ChatCompletionResponse.cs`**: Unified response model (Content, Usage stats, optional Status/IncompleteReason for Responses API, IsSuccess/ErrorMessage for error handling, RawResponseJson/RawRequestJson for debug inspection, optional Refusal for Structured Outputs safety refusals). Provider clients never throw exceptions; errors are returned via `IsSuccess = false` and `ErrorMessage`. Includes a static `Error()` factory method.
    *   **`JsonOutputMode.cs`**: Enum defining JSON output modes: `JsonMode` (json_object, valid JSON without schema enforcement) and `JsonSchema` (json_schema, strict schema-conforming output).
    *   **`JsonOutputOptions.cs`**: Configuration record for JSON output behavior (Mode, SchemaName, SchemaDescription, JsonSchema, Strict). Includes `Validate()` method that throws `ArgumentException` when `JsonSchema` mode is used without required schema fields.
    *   **`EmbeddingRequest.cs`**: Unified request model for embedding operations (Input, Model, InputType, Dimensions, EncodingFormat, ExtraParameters).
    *   **`EmbeddingResponse.cs`**: Unified response model for embeddings (Embeddings, Base64Embeddings, Model, TotalTokens, Dimensions, RawResponseJson, RawRequestJson, IsSuccess/ErrorMessage).
    *   **`MultimodalEmbeddingInput.cs`**: Models for multimodal embedding. `EmbeddingContentPart` (abstract base), `TextEmbeddingContent(Text)`, `ImageEmbeddingContent(ImagePath)`, and `MultimodalEmbeddingInput(Content)` for composing mixed text+image inputs.
    *   **`LlmMessage.cs`**: Represents a message in the conversation (Role, Content).
*   **`JsonDeepMerge.cs`**: Static utility for deeply merging a JSON override document into a base JSON document. Objects are merged recursively; arrays and scalars are replaced by overrides.
*   **`LlmHttpClient.cs`**: Internal helper for handling HTTP requests to the providers. Supports optional `extraParameters` (`JsonElement?`) that are deeply merged into the serialized request payload before sending. `PostWithRawAsync` returns both raw response JSON and raw request JSON for debug inspection.

### Provider Implementations
Each supported provider has its own project providing concrete implementations of the core interfaces.

*   **`src/Cisharpai.OpenAi/`**: Connector for standard OpenAI API. Supports legacy Chat Completions API (GPT-4, etc.), reasoning models (o1/o3/o4), and the Responses API (GPT-5) with status/incomplete handling.
    *   `OpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient` and `IJsonOutputFeature`. Routes to the correct endpoint/format based on model detection. Supports JSON Mode and Structured Outputs across legacy, reasoning, and GPT-5 (Responses API) model paths. Extracts refusal from structured output responses.
    *   `OpenAiEmbeddingClient.cs`: Implements `IEmbeddingClient`.
    *   `Models/OpenAiResponseFormat.cs`: DTOs for `response_format` parameter: `OpenAiResponseFormat`, `OpenAiJsonSchemaSpec` (Chat Completions API), `OpenAiTextFormat` (Responses API with flattened schema structure).
*   **`src/Cisharpai.Azure/`**: Consolidated connector for all Azure AI services. Uses HttpClient directly (no SDK dependencies except Azure.Identity for authentication).
    *   **`Common/`**: Shared utilities for all Azure services.
        *   `AzureClientOptionsBase.cs`: Base class for Azure client configuration (Endpoint, ApiKey, ApiVersion).
        *   `AzureAuthenticationHandler.cs`: DelegatingHandler supporting both API key (`api-key` header) and Azure AD (Bearer token) authentication. Uses scope `https://cognitiveservices.azure.com/.default`.
        *   `AzureErrorMapper.cs`: Static utility for mapping HTTP status codes to user-friendly error messages.
    *   **`AzureOpenAi/`**: Connector for Azure OpenAI Service. Supports both legacy models and reasoning/GPT-5 models (uses `max_completion_tokens` instead of `max_tokens`).
        *   `AzureOpenAiChatCompletionClient.cs`: Implements `IChatCompletionClient` and `IJsonOutputFeature` with Azure-specific auth/routing. Detects reasoning models (o1/o3/o4/gpt-5) and uses appropriate request format. Supports JSON Mode and Structured Outputs. Endpoint: `openai/deployments/{deployment}/chat/completions?api-version=...`.
        *   `Models/AzureOpenAiResponseFormat.cs`: DTOs for `response_format` parameter: `AzureOpenAiResponseFormat`, `AzureOpenAiJsonSchemaSpec`.
        *   `AzureOpenAiEmbeddingClient.cs`: Implements `IEmbeddingClient`. Supports text-embedding-ada-002, text-embedding-3-small, text-embedding-3-large deployments. Endpoint: `openai/deployments/{deployment}/embeddings?api-version=...`.
        *   `AzureOpenAiClientOptions.cs`: Configuration with DeploymentName, extends AzureClientOptionsBase. Default API version: `2024-02-01`.
        *   `Models/`: Request/response DTOs for Azure OpenAI API.
    *   **`AzureAiInference/`**: Connector for Azure AI Inference (model-as-a-service). Supports Phi-3, Llama-3, Mistral, and other Azure AI model catalog offerings, including reasoning models (o1/o3/o4/GPT-5). Uses HttpClient directly (not the Azure.AI.Inference SDK).
        *   `AzureAiInferenceChatCompletionClient.cs`: Implements `IChatCompletionClient` and `IJsonOutputFeature`. Detects reasoning models (o1/o3/o4/gpt-5) and uses appropriate request format (`max_completion_tokens` instead of `max_tokens`, no `Temperature`). Supports JSON Mode and Structured Outputs. Endpoint: `models/chat/completions?api-version=...`.
        *   `Models/AzureAiInferenceResponseFormat.cs`: DTOs for `response_format` parameter: `AzureAiInferenceResponseFormat`, `AzureAiInferenceJsonSchemaSpec`.
        *   `AzureAiInferenceEmbeddingClient.cs`: Implements `IEmbeddingClient` and `IImageEmbeddingFeature`. Supports text and image embeddings. Endpoint: `models/embeddings?api-version=...`.
        *   `AzureAiInferenceClientOptions.cs`: Configuration with ModelId, extends AzureClientOptionsBase. Default API version: `2024-05-01-preview`.
        *   `Models/`: Request/response DTOs for Azure AI Inference API.
    *   **`Extensions/`**: DI service collection extensions.
        *   `AzureOpenAiServiceCollectionExtensions.cs`: `AddAzureOpenAiClient()` for registering Azure OpenAI client.
        *   `AzureAiInferenceServiceCollectionExtensions.cs`: `AddAzureAiInferenceChatCompletion()` and `AddAzureAiInferenceEmbeddings()` for registering Azure AI Inference clients.
*   **`src/Cisharpai.Anthropic/`**: Connector for Anthropic (Claude) API. Supports structured outputs via `output_config.format` parameter.
    *   `AnthropicChatCompletionClient.cs`: Implements `IChatCompletionClient` and `IJsonOutputFeature`. Supports JSON Mode (via system message injection) and Structured Outputs (`json_schema` via `output_config.format`). Handles refusal via `stop_reason: "refusal"`.
    *   `Models/AnthropicOutputConfig.cs`: DTOs for `output_config.format` parameter: `AnthropicOutputConfig`, `AnthropicOutputFormat`.
*   **`src/Cisharpai.Cohere/`**: Connector for Cohere API. Supports Embed v3 and v4 models.
    *   `CohereEmbeddingClient.cs`: Implements `IEmbeddingClient`, `IImageEmbeddingFeature`, and `IMultimodalEmbeddingFeature`. Supports text embeddings, single image embedding, and Embed v4 multimodal embedding (mixed text+image inputs, Matryoshka output dimensions, batch images). Images are sent as data URIs.
    *   `ImageDataUriHelper.cs`: Internal utility for converting image file paths to data URI format (`data:image/{mime};base64,...`). Supports PNG, JPEG, WebP, GIF.
    *   `Models/CohereEmbedInput.cs`: DTOs for Embed v4 `inputs` parameter: `CohereEmbedInput`, `CohereEmbedContentPart`, `CohereImageUrl`.
    *   `Models/CohereEmbedRequest.cs`: Request DTO with `Texts`, `Images`, `Inputs` (v4, mutually exclusive), `InputType`, `EmbeddingTypes`, `OutputDimension` (v4 Matryoshka).
    *   `Models/CohereEmbedResponse.cs`: Response DTO with `CohereEmbeddings`, `CohereBilledUnits` (includes `ImageTokens` for v4), `CohereImageMetadata`.

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
    *   `OpenAiJsonOutputScenario.cs`: OpenAI JSON Mode and Structured Outputs demo.

### Wiki (`wiki/`)
Project documentation pages.

*   `index.md`: Main landing page.
*   `getting-started.md`: Setup and basic usage guide.
*   `openai.md`: OpenAI-specific quickstart.
*   `embeddings.md`: Comprehensive embeddings guide across providers.
*   `feature-extensions.md`: Feature Collection Pattern documentation.
*   `json-output.md`: JSON Mode and Structured Outputs documentation (quick start, provider support matrix, schema guidelines, refusal handling, troubleshooting).

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

### Testing
*   **`src/Cisharpai.Tests.Common/`**: Shared test utilities referenced by all test projects.
    *   `DotEnvLoader.cs`: Static utility class to load environment variables from a `.env` file. Searches current and parent directories.
    *   `TestEnvironmentVariables.cs`: Constants for environment variable names used in integration tests.
*   **`src/Cisharpai.Tests/`**: Unit tests.
    *   `Features/FeatureCollectionTests.cs`: Tests for `FeatureCollection` (Get/Set/enumeration/thread-safety).
    *   `Features/FeatureDiscoveryTests.cs`: Tests verifying feature discovery across all 8 client implementations (including IJsonOutputFeature on OpenAI, Azure OpenAI, Azure AI Inference, and Anthropic; absence on Cohere).
    *   `Cohere/CohereImageEmbeddingTests.cs`: Tests for Cohere image embedding request/response mapping and data URI format.
    *   `Cohere/CohereMultimodalEmbeddingTests.cs`: Tests for Cohere Embed v4 multimodal embedding (text-only, image-only, mixed, batch, output_dimension, input types, raw response, error handling, image tokens).
    *   `Cohere/ImageDataUriHelperTests.cs`: Tests for data URI helper (MIME type mapping for PNG/JPEG/WebP/GIF, base64 encoding).
    *   `Azure/AzureAiInference/AzureAiInferenceImageEmbeddingTests.cs`: Tests for Azure AI Inference image embedding.
    *   `Models/JsonOutputOptionsTests.cs`: Tests for `JsonOutputOptions` validation (JsonMode valid with/without schema, JsonSchema validation scenarios, Strict defaults).
    *   `OpenAi/OpenAiJsonOutputTests.cs`: Tests for OpenAI JSON output request building (JSON Mode for legacy/reasoning/GPT-5, Structured Outputs, refusal handling, system message injection, feature discovery).
    *   `Azure/Common/`: Tests for shared Azure authentication handler and client options.
    *   `Azure/AzureOpenAi/`: Tests for Azure OpenAI client.
    *   `Azure/AzureOpenAi/AzureOpenAiJsonOutputTests.cs`: Tests for Azure OpenAI JSON output request building (JSON Mode, Structured Outputs, refusal, feature discovery, endpoint/header verification).
    *   `Azure/AzureAiInference/`: Tests for Azure AI Inference client.
    *   `Azure/AzureAiInference/AzureAiInferenceJsonOutputTests.cs`: Tests for Azure AI Inference JSON output request building (JSON Mode, Structured Outputs, schema parsing, feature discovery, endpoint/model verification).
    *   `Anthropic/AnthropicJsonOutputTests.cs`: Tests for Anthropic JSON output request building (JSON Mode system message injection, Structured Outputs via output_config, schema parsing, refusal handling, feature discovery).
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
    *   `AzureAiInference/AzureAiInferenceChatCompletionIntegrationTests.cs`: Tests Azure AI Inference models (from `AZURE_INFERENCE_TEST_MODELS` env var, comma-separated).
    *   `AzureAiInference/AzureAiInferenceJsonOutputIntegrationTests.cs`: Integration tests for Azure AI Inference JSON Mode and Structured Outputs (graceful handling for unsupported models, feature discovery).
    *   `Cohere/CohereEmbeddingIntegrationTests.cs`: Integration tests for Cohere text embedding.
    *   `Cohere/CohereImageEmbeddingIntegrationTests.cs`: Integration tests for Cohere image embedding via feature discovery.
    *   `Cohere/CohereMultimodalEmbeddingIntegrationTests.cs`: Integration tests for Cohere Embed v4 multimodal embedding (text-only, image-only, mixed text+image, output dimension control, batch inputs, feature discovery).

# Integration tests

Important: Integration tests has a separate project `src/Cisharpai.Integration.Tests/DotEnv.cs` that is compiled only for .NET 10 to limit the number of call to real provdier.

**Maintenance Note:** When adding or modifying environment variables for integration tests, update the following files to keep them in sync:
1. `src/Cisharpai.Tests.Common/TestEnvironmentVariables.cs` - Add the constant for the new variable
2. `src/Cisharpai.Integration.Tests/DotEnv.cs` - Re-export the constant from `TestEnvironmentVariables` for backwards compatibility
3. `src/Cisharpai.Integration.Tests/EnvironmentConfigurationTests.cs` - Add to the validation array
4. `scripts/gh-secrets-from-dotenv.zsh` - Add to the `allowlist` array (for GitHub Actions/Codespaces secrets)
5. `memories/project_overview.md` - Update the `.env` example above
