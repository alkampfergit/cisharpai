# Provider Feature Support Matrix

## Feature Matrix

| Feature | OpenAI | Azure OpenAI | Azure AI Inference | Anthropic | Cohere |
|---------|--------|--------------|-------------------|-----------|--------|
| Chat Completions | Yes | Yes | Yes | Yes | Yes |
| Text Embeddings | Yes | Yes | Yes | -- | Yes |
| Reranking | -- | -- | -- | -- | Yes |
| JSON Mode | Yes | Yes | Yes | Yes | Yes |
| Structured Outputs | Yes | Yes | Varies | Yes | Yes |
| Image Embeddings | -- | -- | Yes | -- | Yes |
| Multimodal Embeddings | -- | -- | -- | -- | Yes |
| Grounded Chat (RAG) | Yes (GPT-5) | Yes (GPT-5) | -- | Yes | Yes |
| Tool Calling | Yes | Yes | Yes | Yes | Yes |
| Vision | Yes | Yes | Varies | Yes | Partial |
| Streaming | Yes | Yes | Yes | Yes | Yes |

## Feature Discovery Pattern

```csharp
// All optional features follow this pattern
var feature = client.Features.Get<IFeatureInterface>();
if (feature is not null)
{
    // Provider supports this feature
}
```

## Feature Interfaces

| Interface | What It Does |
|-----------|-------------|
| `IJsonOutputFeature` | JSON Mode + Structured Outputs |
| `IToolCallingFeature` | Function/tool calling |
| `IStreamingChatFeature` | Token-by-token streaming |
| `IGroundedChatFeature` | Document-grounded RAG with citations |
| `IImageEmbeddingFeature` | Single image embedding |
| `IMultimodalEmbeddingFeature` | Mixed text + image embedding |

Reranking is not discovered through `Features` — it is the standalone `IRerankerClient`
interface, implemented by Cohere only.

## Provider-Specific Notes

### OpenAI
- Model routing: legacy (GPT-4), reasoning (o1/o3/o4), Responses API (GPT-5)
- Full feature support except embeddings are text-only

### Azure OpenAI
- Deployment-based routing, API key + Azure AD auth
- `ModelName` can describe the underlying OpenAI model when `DeploymentName` is opaque; learned route mismatches are cached in-process per `(Endpoint, DeploymentName, ApiVersion)`
- Same feature set as OpenAI

### Azure AI Inference
- Model catalog — features depend on deployed model
- Only provider with image embeddings (besides Cohere)

### Anthropic
- Grounded chat via `document` content blocks with native citations; `CitedText` on `CitationSource`
- Event-based SSE streaming (not `[DONE]` based)
- Raw base64 images (not data URIs)
- Claude model family only

### Cohere
- Most feature-rich: grounded chat, image/multimodal embeddings, reranking (`IRerankerClient`)
- Vision is partial (image parts silently skipped in chat)
- Uppercase ToolChoice values, `Specific` degrades to `REQUIRED`
- Embedding InputType required for v3 models
