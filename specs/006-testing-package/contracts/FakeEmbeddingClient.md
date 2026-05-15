# Contract: FakeEmbeddingClient

**File**: `src/Cisharpai.Testing/FakeEmbeddingClient.cs`

## Class Definition

```csharp
public sealed class FakeEmbeddingClient :
    IEmbeddingClient,
    IImageEmbeddingFeature,
    IMultimodalEmbeddingFeature
```

## Behavior

### Construction

`FakeEmbeddingClient(FakeEmbeddingFeatures enabledFeatures = FakeEmbeddingFeatures.All)`

Creates a new fake client. For each flag set in `enabledFeatures`, the client registers itself into its `FeatureCollection`. By default both features are registered.

### Response Resolution

Same queue-then-default pattern as `FakeChatCompletionClient`:
1. Capture the incoming request into the corresponding capture list.
2. If the feature-specific queue has items, dequeue and return.
3. If the feature-specific default is set, return it.
4. For `GetImageEmbeddingAsync`: falls back to `DefaultResponse` when `DefaultImageResponse` is null.
5. For `GetMultimodalEmbeddingsAsync`: falls back to `DefaultResponse` when `DefaultMultimodalResponse` is null.
6. If no response is available, throw `InvalidOperationException`.

### Methods

| Method | Queue | Default | Fallback | Capture |
|--------|-------|---------|----------|---------|
| `GetEmbeddingsAsync` | `EnqueueResponse()` | `DefaultResponse` | — | `ReceivedRequests` |
| `GetImageEmbeddingAsync` | `EnqueueImageResponse()` | `DefaultImageResponse` | `DefaultResponse` | `ReceivedImageRequests` |
| `GetMultimodalEmbeddingsAsync` | `EnqueueMultimodalResponse()` | `DefaultMultimodalResponse` | `DefaultResponse` | `ReceivedMultimodalRequests` |

### Capture Details

- `ReceivedRequests`: `IReadOnlyList<EmbeddingRequest>` — captures `GetEmbeddingsAsync` requests
- `ReceivedImageRequests`: `IReadOnlyList<(string ImagePath, string Model)>` — captures image path and model
- `ReceivedMultimodalRequests`: `IReadOnlyList<IReadOnlyList<MultimodalEmbeddingInput>>` — captures input lists

### Reset

`Reset()` clears all 3 queues and all 3 capture lists. Does NOT clear default response properties.

### CallCount

`CallCount` returns the sum of all capture list counts (embedding + image + multimodal).

## Discovery

```csharp
var fake = new FakeEmbeddingClient();
var image = fake.Features.Get<IImageEmbeddingFeature>();         // returns this
var multimodal = fake.Features.Get<IMultimodalEmbeddingFeature>(); // returns this
```

```csharp
var imageOnly = new FakeEmbeddingClient(FakeEmbeddingFeatures.ImageEmbedding);
var image = imageOnly.Features.Get<IImageEmbeddingFeature>();         // returns this
var multimodal = imageOnly.Features.Get<IMultimodalEmbeddingFeature>(); // returns null
```
