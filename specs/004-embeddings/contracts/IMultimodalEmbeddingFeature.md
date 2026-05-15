# Contract: IMultimodalEmbeddingFeature

**File**: `src/Cisharpai/Features/Embeddings/IMultimodalEmbeddingFeature.cs`

## Interface Definition

```csharp
public interface IMultimodalEmbeddingFeature
{
    Task<EmbeddingResponse> GetMultimodalEmbeddingsAsync(
        IReadOnlyList<MultimodalEmbeddingInput> inputs,
        string model,
        EmbeddingInputType? inputType = null,
        int? outputDimension = null,
        bool includeRawResponse = false,
        JsonElement? extraParameters = null,
        CancellationToken cancellationToken = default);
}
```

## Behavior

### `GetMultimodalEmbeddingsAsync`

Generates vector embeddings from mixed text-and-image inputs. Each `MultimodalEmbeddingInput` can contain any combination of `TextEmbeddingContent` and `ImageEmbeddingContent` parts.

**Input**:
- `inputs`: One or more multimodal input containers, each with a list of content parts.
- `model`: The embedding model (must support multimodal input, e.g., Cohere Embed v4).
- `inputType`: Optional semantic hint (query, document, classification, clustering).
- `outputDimension`: Optional Matryoshka dimension control. When set, the provider returns vectors of this length. When null, omitted from the request.
- `includeRawResponse`: When true, populates `RawResponseJson`/`RawRequestJson` on the response.
- `extraParameters`: Deep-merged into the provider request JSON.

**Output**: `EmbeddingResponse` — one embedding vector per input, ordered by input index.

**Error behavior**:
- HTTP errors: Returns `EmbeddingResponse` with `IsSuccess=false`.
- Network failures: Caught and returned as error response.

**Content part handling**:
- `TextEmbeddingContent`: Serialized as `{type: "text", text: "..."}`.
- `ImageEmbeddingContent`: Image file is read and converted to a data URI, then serialized as `{type: "image_url", image_url: {url: "data:..."}}`.

## Discovery

This feature is optional. Discover via Feature Collection:

```csharp
if (client.Features.Get<IMultimodalEmbeddingFeature>() is { } multimodal)
{
    var inputs = new List<MultimodalEmbeddingInput>
    {
        new([
            new TextEmbeddingContent("A photo of a cat"),
            new ImageEmbeddingContent("cat.png")
        ])
    };

    var response = await multimodal.GetMultimodalEmbeddingsAsync(inputs, "embed-v4.0");
}
```

## Availability

| Provider | Available | Notes |
|----------|-----------|-------|
| OpenAI | No | — |
| Azure OpenAI | No | — |
| Azure AI Inference | No | Image embeddings use `IImageEmbeddingFeature` (single image, no mixed inputs) |
| Cohere | Yes | Only provider supporting mixed text+image in a single embedding request |
| Fake (testing) | Configurable | Enabled with `FakeEmbeddingFeatures.MultimodalEmbedding` |
