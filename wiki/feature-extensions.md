# Feature: Extension System & Capability Discovery

## Overview
As the library integrates more diverse AI providers (OpenAI, Anthropic, Cohere, etc.), we face the challenge of "feature fragmentation." Some providers support multimodal input, JSON mode, or caching, while others do not. We need a unified way to:
1.  Discover which features a generic client instance supports.
2.  Access those specialized features in a type-safe manner.
3.  Avoid polluting the core interfaces (`IChatCompletionClient`, `IEmbeddingClient`) with provider-specific methods.

## Proposed Architecture: The Feature Collection Pattern

We will implement a **Features Pattern** similar to ASP.NET Core's `HttpContext.Features` or `HttpClient` middleware. This allows for flexible, composition-based extension without breaking changes.

### Core Structure

```csharp
// 1. The container interface
public interface IHasFeatures
{
    IFeatureCollection Features { get; }
}

public interface IFeatureCollection : IEnumerable<KeyValuePair<Type, object>>
{
    T? Get<T>();
    void Set<T>(T instance);
}

// 2. Base clients implement this
public interface IEmbeddingClient : IHasFeatures
{
    Task<EmbeddingResponse> GetEmbeddingsAsync(EmbeddingRequest request, CancellationToken cancellationToken = default);
}

// 3. Define the specific feature capability
public interface IImageEmbeddingFeature
{
    /// <summary>
    /// Embeds a single image.
    /// </summary>
    /// <param name="imagePath">Local file path.</param>
    /// <param name="model">The model to use (must be multimodal compatible).</param>
    Task<EmbeddingResponse> GetImageEmbeddingAsync(string imagePath, string model, CancellationToken cancellationToken = default);
}
```

### Usage Example

```csharp
public async Task ProcessInputs(IEmbeddingClient client)
{
    // Standard text embedding (guaranteed by IEmbeddingClient)
    await client.GetEmbeddingsAsync(new EmbeddingRequest(["Hello world"], "model-id"));

    // Feature Discovery
    // Note: The presence of this feature indicates the CLIENT was configured
    // to allow image inputs for the configured model set.
    if (client.Features.Get<IImageEmbeddingFeature>() is { } imageFeature)
    {
        // Use local file path
        await imageFeature.GetImageEmbeddingAsync("path/to/image.png", "clip-model");
    }
    else
    {
        Console.WriteLine("This provider/configuration does not support image embeddings.");
    }
}

```

## Implementation Plan

### Phase 1: Core Infrastructure
- Define `IFeatureCollection` and basic implementation `FeatureCollection`.
- Update `IEmbeddingClient` and `IChatCompletionClient` to inherit from `IHasFeatures`.
- Update base implementations (if any) or add default (empty) feature collections to existing clients.

### Phase 2: Feature Definitions
- Define `IImageEmbeddingFeature` for multimodal embedding.
- Add a follow-up to extend `IImageEmbeddingFeature` with `Stream` input.
- Define `IToolCallingFeature` (if we want to extract that from core chat in the future, or advanced tool features).
- Define `IJsonOutputFeature` for JSON Mode and Structured Outputs (see [json-output.md](json-output.md)).

### Phase 3: Provider Implementation
- **Cohere**: Implement `IImageEmbeddingFeature` in `CohereEmbeddingClient`.
- **Azure AI Inference**: Implement `IImageEmbeddingFeature`.
- **OpenAI**: Does not support image embedding currently.

## Testing
- **Unit**: Verify `IImageEmbeddingFeature` is only exposed when configured with image-capable models.
- **Integration (Cohere)**: Use `embed-v4-0` with a local image path and assert:
    - `EmbeddingResponse.IsSuccess` is true.
    - `Embeddings` is not empty and dimensions are consistent.
    - `TotalTokens` is populated.
- **Integration (Azure AI Inference)**: Validate image embedding with a known image-capable model.

## Task Breakdown

### Task 1: Scaffolding
- [ ] Create `Cisharpai/Features/IFeatureCollection.cs`
- [ ] Create `Cisharpai/Features/FeatureCollection.cs` implementation.
- [ ] Create `Cisharpai/Features/IHasFeatures.cs`
- [ ] Modify `IEmbeddingClient.cs` to inherit `IHasFeatures`.
- [ ] Modify `IChatCompletionClient.cs` to inherit `IHasFeatures`.

### Task 2: Implementation Updates
- [ ] Update `AnthropicChatCompletionClient` to initialize `Features`.
- [ ] Update `CohereEmbeddingClient` to initialize `Features`.
- [ ] Update `OpenAiChatCompletionClient` to initialize `Features`.
- [ ] Update `OpenAiEmbeddingClient` to initialize `Features`.

### Task 3: Image Embedding Feature
- [ ] Create `Cisharpai/Features/Embeddings/IImageEmbeddingFeature.cs`.
- [ ] Implement `IImageEmbeddingFeature` in `CohereEmbeddingClient`.
- [ ] Implement `IImageEmbeddingFeature` in Azure AI Inference client.
- [ ] Add integration test verifying feature discovery works.
