# Azure Providers

Cisharpai supports two distinct Azure AI services in the `Cisharpai.Azure` package.

## Azure OpenAI

### Setup

```csharp
services.AddAzureOpenAiClient(options =>
{
    options.Endpoint = "https://myresource.openai.azure.com";
    options.DeploymentName = "gpt-4o";
    options.ApiKey = "...";         // API key auth
    // OR
    options.TokenCredential = new DefaultAzureCredential(); // Azure AD
    options.DefaultModel = "gpt-4o"; // optional
});
```

**Options:**
- `Endpoint` (required) — Azure resource endpoint
- `DeploymentName` (required) — Deployment name
- `ApiKey` — For API key authentication
- `TokenCredential` — For Azure AD authentication (Azure.Identity)
- `DefaultModel` — Fallback model name

### Endpoint Format

Requests route to: `{Endpoint}/openai/deployments/{DeploymentName}/chat/completions?api-version=...`

### Supported Features

- `IJsonOutputFeature` — JSON Mode + Structured Outputs
- `IToolCallingFeature` — Function calling
- `IStreamingChatFeature` — SSE streaming, `[DONE]` terminator
- Vision — Data URIs
- Text Embeddings — With dimension reduction support

### Authentication

Two modes, mutually exclusive:
1. **API Key** — `api-key` header
2. **Azure AD** — Bearer token via `TokenCredential` (recommended for production)

---

## Azure AI Inference

### Setup

```csharp
services.AddAzureAiInferenceClient(options =>
{
    options.Endpoint = "https://myendpoint.inference.ai.azure.com";
    options.ModelId = "Phi-4";
    options.ApiKey = "...";
    // OR
    options.TokenCredential = new DefaultAzureCredential();
});
```

**Options:**
- `Endpoint` (required) — Inference endpoint
- `ModelId` (required) — Model identifier
- `ApiKey` or `TokenCredential` — Authentication

### Endpoint Format

Requests route to: `{Endpoint}/models/{ModelId}/chat/completions`

### Supported Features

- `IJsonOutputFeature` — JSON Mode + Structured Outputs (model-dependent)
- `IToolCallingFeature` — Function calling (model-dependent)
- `IStreamingChatFeature` — SSE streaming
- `IImageEmbeddingFeature` — Single image embedding
- Vision — Model-dependent (Phi-3-vision, Llama-3.2-vision)

### Available Models

This is a model catalog service. Capabilities depend on the deployed model:
- **Phi-4** — Chat, tool calling
- **Phi-3-vision** — Chat, vision
- **Llama-3.2** — Chat, some support tool calling
- **Mistral** — Chat, tool calling
- Various embedding models with image support

### Image Embeddings

```csharp
var imageFeature = client.Features.Get<IImageEmbeddingFeature>();
var response = await imageFeature.GetImageEmbeddingAsync(
    imagePath: "photo.png",
    model: "Phi-3-vision");
```

## DI with Keyed Services

Both Azure providers support keyed DI for multi-provider scenarios:

```csharp
services.AddAzureOpenAiClient("primary", o => { ... });
services.AddAzureOpenAiClient("secondary", o => { ... });

// Resolve
var client = provider.GetRequiredKeyedService<IChatCompletionClient>("primary");
```
