# Feature Collection Pattern

Cisharpai uses the Feature Collection pattern — inspired by ASP.NET Core's `HttpContext.Features` — to expose optional, provider-specific capabilities without polluting the core `IChatCompletionClient` and `IEmbeddingClient` interfaces.

## Core Interfaces

```csharp
public interface IHasFeatures
{
    IFeatureCollection Features { get; }
}

public interface IFeatureCollection : IEnumerable<KeyValuePair<Type, object>>
{
    T? Get<T>();
    void Set<T>(T instance);
}
```

Both `IChatCompletionClient` and `IEmbeddingClient` inherit `IHasFeatures`. The underlying `FeatureCollection` implementation is backed by a `ConcurrentDictionary` and is thread-safe.

## Feature Discovery

Call `Features.Get<T>()` to retrieve a feature. A `null` return means the provider or its current configuration does not support that capability.

```csharp
// Always null-check before use
if (client.Features.Get<IStreamingChatFeature>() is { } streamFeature)
{
    await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
        Console.Write(chunk.Content);
}
else
{
    Console.WriteLine("This provider does not support streaming.");
}
```

The `is { }` pattern is the idiomatic way to null-check and bind in one step.

## Available Features

### Chat Features (on `IChatCompletionClient`)

| Feature Interface | Description | Providers |
|------------------|-------------|-----------|
| `IJsonOutputFeature` | JSON Mode and Structured Outputs | All 5 |
| `IToolCallingFeature` | Function/tool calling | All 5 |
| `IStreamingChatFeature` | Token-by-token streaming via `IAsyncEnumerable` | All 5 |
| `IGroundedChatFeature` | RAG with document citations | Anthropic, Cohere |

### Embedding Features (on `IEmbeddingClient`)

| Feature Interface | Description | Providers |
|------------------|-------------|-----------|
| `IImageEmbeddingFeature` | Single image embedding | Azure AI Inference, Cohere |
| `IMultimodalEmbeddingFeature` | Mixed text + image embedding | Cohere only |

## Checking Multiple Features

```csharp
IChatCompletionClient client = /* any provider */;

var jsonFeature    = client.Features.Get<IJsonOutputFeature>();
var toolFeature    = client.Features.Get<IToolCallingFeature>();
var streamFeature  = client.Features.Get<IStreamingChatFeature>();
var groundedFeature = client.Features.Get<IGroundedChatFeature>();

Console.WriteLine($"JSON output:    {jsonFeature is not null}");
Console.WriteLine($"Tool calling:   {toolFeature is not null}");
Console.WriteLine($"Streaming:      {streamFeature is not null}");
Console.WriteLine($"Grounded chat:  {groundedFeature is not null}");
```

## Writing Provider-Agnostic Code

Feature discovery is the right way to write code that degrades gracefully across providers:

```csharp
public static async Task<string> GetCompletionAsync(
    IChatCompletionClient client,
    ChatCompletionRequest request)
{
    // Prefer streaming when available
    if (client.Features.Get<IStreamingChatFeature>() is { } streaming)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in streaming.GetChatCompletionStreamAsync(request))
            sb.Append(chunk.Content);
        return sb.ToString();
    }

    // Fall back to non-streaming
    var response = await client.GetChatCompletionAsync(request);
    return response.IsSuccess ? response.Content : throw new InvalidOperationException(response.ErrorMessage);
}
```

## Testing with Feature Discovery

When using `FakeChatCompletionClient`, you control which features are exposed via the `FakeChatFeatures` flags:

```csharp
// Only streaming + tool calling exposed
var fake = new FakeChatCompletionClient(FakeChatFeatures.Streaming | FakeChatFeatures.ToolCalling);

Assert.NotNull(fake.Features.Get<IStreamingChatFeature>());
Assert.NotNull(fake.Features.Get<IToolCallingFeature>());
Assert.Null(fake.Features.Get<IJsonOutputFeature>());  // not registered

// Test that your code handles missing features gracefully
var fake2 = new FakeChatCompletionClient(FakeChatFeatures.None);
Assert.Null(fake2.Features.Get<IStreamingChatFeature>());
```

See [Testing with Cisharpai](testing.md) for the full fake client reference.

## See Also

- [Provider Feature Matrix](provider-features.md) — which providers support which features
- [Tool Calling](tool-calling.md) — `IToolCallingFeature` guide
- [Streaming](streaming.md) — `IStreamingChatFeature` guide
- [JSON Output](json-output.md) — `IJsonOutputFeature` guide
- [Grounded Chat (RAG)](grounded-chat.md) — `IGroundedChatFeature` guide
- [Embeddings](embeddings.md) — `IImageEmbeddingFeature` and `IMultimodalEmbeddingFeature`
