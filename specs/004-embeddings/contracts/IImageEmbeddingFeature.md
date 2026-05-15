# Contract: IImageEmbeddingFeature

**File**: `src/Cisharpai/Features/Embeddings/IImageEmbeddingFeature.cs`

## Interface Definition

```csharp
public interface IImageEmbeddingFeature
{
    Task<EmbeddingResponse> GetImageEmbeddingAsync(
        string imagePath,
        string model,
        CancellationToken cancellationToken = default);
}
```

## Behavior

### `GetImageEmbeddingAsync`

Generates a vector embedding from a single local image file.

**Input**:
- `imagePath`: Absolute or relative path to a local image file. Supported formats: PNG, JPEG, WebP, GIF. The file is read and converted to a base64 data URI at send time.
- `model`: The embedding model to use (must be multimodal-capable).

**Output**: `EmbeddingResponse` — same unified response as text embeddings.

**Error behavior**:
- Empty `imagePath`: Throws `ArgumentException`.
- HTTP errors: Returns `EmbeddingResponse` with `IsSuccess=false`.
- File not found: Propagates `FileNotFoundException` (not an API error).

**Provider-specific serialization**:
- Azure AI Inference: Sends to `models/images/embeddings` endpoint with `input: [{image: "data:image/...;base64,..."}]`.
- Cohere: Sends to `embed` endpoint with `images: ["data:image/...;base64,..."]` and `input_type: "image"`.

## Discovery

This feature is optional. Discover via Feature Collection:

```csharp
if (client.Features.Get<IImageEmbeddingFeature>() is { } imageFeature)
{
    var response = await imageFeature.GetImageEmbeddingAsync("photo.png", "clip-model");
}
```

## Availability

| Provider | Available | Notes |
|----------|-----------|-------|
| OpenAI | No | OpenAI does not offer image embedding models |
| Azure OpenAI | No | Azure OpenAI deployments are text-only for embeddings |
| Azure AI Inference | Yes | Uses separate `/models/images/embeddings` endpoint |
| Cohere | Yes | Uses Embed v4 models with `images` array |
| Fake (testing) | Configurable | Enabled with `FakeEmbeddingFeatures.ImageEmbedding` |
