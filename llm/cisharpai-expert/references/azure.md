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
    options.DefaultModel = "gpt-4o";    // optional fallback when request omits Model
    options.ModelFamily = "gpt-5";       // optional, only when DeploymentName is opaque
    options.ReasoningEffort = "medium";  // optional, o-series and gpt-5
    options.TextVerbosity = "high";      // optional, gpt-5 only
});
```

**Options:**
- `Endpoint` (required) — Azure resource endpoint
- `DeploymentName` (required) — Deployment name (used in URL)
- `ApiKey` — For API key authentication
- `TokenCredential` — For Azure AD authentication (Azure.Identity)
- `DefaultModel` — Fallback model name when `request.Model` is null
- `ModelFamily` — Optional explicit routing hint when `DeploymentName` is opaque (see [Model Routing](#model-routing))
- `ReasoningEffort` — `"low"` / `"medium"` / `"high"` for o-series and gpt-5
- `TextVerbosity` — `"low"` / `"high"` for gpt-5 (Responses API)

### Model Routing

The client automatically picks the API surface based on the model family:

| Model Pattern | API Route | URL |
|---------------|-----------|-----|
| `gpt-5*` | Responses API | `/openai/deployments/{DeploymentName}/responses?api-version=...` |
| `o1*`, `o3*`, `o4*` | Chat Completions (reasoning) | `/openai/deployments/{DeploymentName}/chat/completions?api-version=...` |
| Everything else | Chat Completions (standard) | `/openai/deployments/{DeploymentName}/chat/completions?api-version=...` |

**Resolution order for the routing hint:** `options.ModelFamily` → `request.Model` → `options.DefaultModel` → standard Chat Completions for unknown prefixes.

#### Opaque Deployment Names (`ModelFamily`)

Azure deployment names are arbitrary — `DeploymentName = "foo"` may host gpt-5 underneath. Set `ModelFamily` to make routing explicit:

```csharp
services.AddAzureOpenAiClient(o =>
{
    o.Endpoint = "...";
    o.DeploymentName = "foo";   // opaque name; URL uses this verbatim
    o.ApiKey = "...";
    o.ModelFamily = "gpt-5";    // routes via Responses API regardless of deployment name
});
```

If `ModelFamily` is left empty and the deployment name does not match a known prefix, the client falls back to the standard Chat Completions API.

### Responses API (GPT-5)

GPT-5 deployments route through the Responses API automatically. Tool calling with GPT-5 still uses Chat Completions (matches the OpenAI client's behaviour).

```csharp
services.AddAzureOpenAiClient(o =>
{
    o.Endpoint = "...";
    o.DeploymentName = "gpt-5-prod";
    o.ApiKey = "...";
    o.ApiVersion = "2025-04-01-preview"; // a Responses-API-capable api-version
    o.TextVerbosity = "high";
    o.ReasoningEffort = "medium";
});
```

Streaming uses `response.output_text.delta` and `response.completed` events (not `[DONE]`).

### Supported Features

- `IJsonOutputFeature` — JSON Mode + Structured Outputs (Chat Completions and Responses API)
- `IToolCallingFeature` — Function calling (Chat Completions for all model families, including gpt-5)
- `IStreamingChatFeature` — SSE streaming
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
