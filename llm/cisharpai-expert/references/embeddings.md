# Embeddings

## Text Embeddings

```csharp
var client = provider.GetRequiredService<IEmbeddingClient>();
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world", "Another text"],
    Model: "text-embedding-3-small",
    Dimensions: 256)); // optional dimension reduction

if (response.IsSuccess)
{
    foreach (var embedding in response.Embeddings)
        Console.WriteLine($"Vector: [{string.Join(", ", embedding.Take(5))}...]");
}
```

## Image Embeddings

Available on: Azure AI Inference, Cohere

```csharp
var imageFeature = client.Features.Get<IImageEmbeddingFeature>();
var response = await imageFeature.GetImageEmbeddingAsync(
    imagePath: "photo.png",
    model: "embed-v4.0");
```

## Multimodal Embeddings (Cohere Embed v4)

```csharp
var multiFeature = client.Features.Get<IMultimodalEmbeddingFeature>();
var response = await multiFeature.GetMultimodalEmbeddingsAsync(
    inputs:
    [
        new MultimodalEmbeddingInput(
        [
            new TextEmbeddingContent("A cat"),
            new ImageEmbeddingContent("cat.png")
        ])
    ],
    model: "embed-v4.0");
```

## Provider-Specific Notes

### OpenAI
- Models: `text-embedding-3-small`, `text-embedding-3-large`
- Supports dimension reduction via `Dimensions`

### Azure OpenAI
- Same models as OpenAI, deployment-based routing
- Supports dimension reduction

### Azure AI Inference
- Model-dependent capabilities
- Supports `IImageEmbeddingFeature` for models that handle images

### Cohere
- **InputType is REQUIRED** for v3 models: `search_query`, `search_document`, `classification`, `clustering`
- **Image format:** Must be data URIs (`data:image/png;base64,...`). Raw base64 returns 422.
- **Minimum image size:** 64x64 pixels minimum (1x1 fails)
- **Mutually exclusive:** `images`, `inputs`, `texts` fields cannot be combined in same request
- Models: `embed-english-v3.0`, `embed-multilingual-v3.0`, `embed-v4.0`

## EmbeddingRequest Properties

- `Input` — String array of texts
- `Model` — Model name
- `InputType` — Required for Cohere v3
- `Dimensions` — Reduce output dimensions (OpenAI, Azure)
- `EncodingFormat` — `float` (default) or `base64`
- `ExtraParameters` — Provider-specific overrides
- `IncludeRawResponse` — Include raw JSON for debugging

## EmbeddingResponse Properties

- `Embeddings` — `float[][]` result vectors
- `Dimensions` — Actual output dimensions
- `Model` — Model used
- `TotalTokens` — Tokens consumed
- `IsSuccess` / `ErrorMessage` — Error handling (no exceptions)
