# Contract: FakeChatCompletionClient

**File**: `src/Cisharpai.Testing/FakeChatCompletionClient.cs`

## Class Definition

```csharp
public sealed class FakeChatCompletionClient :
    IChatCompletionClient,
    IStreamingChatFeature,
    IToolCallingFeature,
    IJsonOutputFeature,
    IGroundedChatFeature
```

## Behavior

### Construction

`FakeChatCompletionClient(FakeChatFeatures enabledFeatures = FakeChatFeatures.All)`

Creates a new fake client. For each flag set in `enabledFeatures`, the client registers itself into its `FeatureCollection`. By default all four features are registered.

### Response Resolution

All methods follow the same pattern:
1. Capture the incoming request (and options, if applicable) into the corresponding capture list.
2. If the feature-specific queue has items, dequeue and return the next one.
3. If the feature-specific default is set, return it.
4. For `GetChatCompletionWithJsonOutputAsync` only: if `DefaultJsonOutputResponse` is null, fall back to `DefaultResponse`.
5. If no response is available, throw `InvalidOperationException` with a descriptive message.

### Methods

| Method | Queue | Default | Fallback | Capture |
|--------|-------|---------|----------|---------|
| `GetChatCompletionAsync` | `EnqueueResponse()` | `DefaultResponse` | — | `ReceivedRequests` |
| `GetChatCompletionWithJsonOutputAsync` | `EnqueueJsonOutputResponse()` | `DefaultJsonOutputResponse` | `DefaultResponse` | `ReceivedJsonOutputRequests` |
| `GetChatCompletionWithToolsAsync` | `EnqueueToolCallingResponse()` | `DefaultToolCallingResponse` | — | `ReceivedToolCallingRequests` |
| `GetGroundedChatCompletionAsync` | `EnqueueGroundedChatResponse()` | `DefaultGroundedChatResponse` | — | `ReceivedGroundedChatRequests` |
| `GetChatCompletionStreamAsync` | `EnqueueStreamingResponse()` | `DefaultStreamingResponse` | — | `ReceivedStreamingRequests` |

### Streaming Behavior

`GetChatCompletionStreamAsync` yields chunks from the resolved list asynchronously, calling `cancellationToken.ThrowIfCancellationRequested()` between chunks and `await Task.Yield()` to simulate async behavior.

### Reset

`Reset()` clears all 5 queues and all 5 capture lists. Does NOT clear default response properties.

### CallCount

`CallCount` returns the sum of all capture list counts (chat + tool calling + JSON output + grounded chat + streaming).

## Discovery

```csharp
var fake = new FakeChatCompletionClient();
var streaming = fake.Features.Get<IStreamingChatFeature>();     // returns this
var toolCalling = fake.Features.Get<IToolCallingFeature>();       // returns this
var jsonOutput = fake.Features.Get<IJsonOutputFeature>();         // returns this
var grounded = fake.Features.Get<IGroundedChatFeature>();         // returns this
```

```csharp
var bare = new FakeChatCompletionClient(FakeChatFeatures.None);
var streaming = bare.Features.Get<IStreamingChatFeature>();       // returns null
```
