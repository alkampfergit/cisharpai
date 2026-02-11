# Provider Feature Matrix

This page lists every feature supported by each provider integration in Cisharpai. Keep it up-to-date whenever a feature is added or removed.

## Feature Legend

| Feature | Interface | Description |
|---------|-----------|-------------|
| Chat Completions | `IChatCompletionClient` | Send messages and receive model-generated replies |
| Text Embeddings | `IEmbeddingClient` | Generate vector embeddings from text |
| JSON Output | `IJsonOutputFeature` | Force JSON Mode or Structured Outputs on chat responses |
| Image Embeddings | `IImageEmbeddingFeature` | Generate vector embeddings from a single image |
| Multimodal Embeddings | `IMultimodalEmbeddingFeature` | Embed mixed text + image inputs in a single request |
| Grounded Chat (RAG) | `IGroundedChatFeature` | Chat with document grounding and citations |
| Tool Calling | `IToolCallingFeature` | Function calling / tool use in chat completions |

## Support Matrix

| Feature | OpenAI | Azure OpenAI | Azure AI Inference | Anthropic | Cohere |
|---------|--------|--------------|-------------------|-----------|--------|
| Chat Completions | Yes | Yes | Yes | Yes | Yes |
| Text Embeddings | Yes | Yes | Yes | -- | Yes |
| JSON Mode | Yes | Yes | Yes | Yes | Yes |
| Structured Outputs | Yes | Yes | Yes | Yes | Yes |
| Image Embeddings | -- | -- | Yes | -- | Yes |
| Multimodal Embeddings | -- | -- | -- | -- | Yes |
| Reasoning Models | Yes | Yes | Yes | -- | -- |
| Responses API (GPT-5) | Yes | -- | -- | -- | -- |
| Grounded Chat (RAG) | -- | -- | -- | -- | Yes |
| Tool Calling | Yes | -- | -- | Yes | Yes |

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

### Azure OpenAI

**Package:** `Cisharpai.Azure`

| Capability | Details |
|------------|---------|
| Chat Completions | Deployment-based routing; reasoning-model detection (o1/o3/o4/gpt-5) |
| Text Embeddings | text-embedding-ada-002, text-embedding-3-small, text-embedding-3-large |
| JSON Mode | Via `response_format` (requires api-version 2024-08-01-preview+ for json_schema) |
| Structured Outputs | Via `response_format.json_schema`; refusal extraction supported |
| Reasoning Models | Detected automatically; uses `max_completion_tokens` instead of `max_tokens` |
| Authentication | API key (`api-key` header) or Azure AD (Bearer token) |

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
| Authentication | API key (`api-key` header) or Azure AD (Bearer token) |

### Anthropic

**Package:** `Cisharpai.Anthropic`

| Capability | Details |
|------------|---------|
| Chat Completions | Claude model family (claude-opus-4-5, claude-sonnet-4-5, claude-haiku-4-5) |
| JSON Mode | Implemented via system-message injection; auto-strips markdown fences |
| Structured Outputs | Via native `output_config.format` parameter; refusal via `stop_reason: "refusal"` |
| Tool Calling | All Claude models; `ToolChoice` maps Auto->auto, Required->any, Specific->{type:tool,name}, None is omitted |

### Cohere

**Package:** `Cisharpai.Cohere`

| Capability | Details |
|------------|---------|
| Chat Completions | Command family models (command-a-03-2025, command-r-plus-08-2024, command-r-08-2024) |
| Text Embeddings | Embed v3 and v4 models |
| JSON Mode | Via `response_format` type `json_object` |
| Structured Outputs | Via `response_format` with `json_schema` parameter |
| Image Embeddings | Single image via data URI (`data:image/{mime};base64,...`) |
| Multimodal Embeddings | Embed v4 mixed text + image inputs, Matryoshka dimension control, batch images |
| Supported Formats | PNG, JPEG, WebP, GIF |
| Grounded Chat (RAG) | Document grounding via `documents` array, `citation_options` mode (ACCURATE/FAST/ENABLED), citation character offsets |
| Input Types | search_query, search_document, classification, clustering |
| Tool Calling | Command models; `ToolChoice` maps to uppercase (AUTO/NONE/REQUIRED); Specific degrades to REQUIRED; `strict_tools` flag when all tools are strict |

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
