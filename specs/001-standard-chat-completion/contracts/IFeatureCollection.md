# Contract: IFeatureCollection & IHasFeatures

**Files**:
- `src/Cisharpai/Features/IHasFeatures.cs`
- `src/Cisharpai/Features/IFeatureCollection.cs`
- `src/Cisharpai/Features/FeatureCollection.cs`

## Interface Definitions

```csharp
public interface IHasFeatures
{
    IFeatureCollection Features { get; }
}

public interface IFeatureCollection : IEnumerable<KeyValuePair<Type, object>>
{
    T? Get<T>() where T : class;
    void Set<T>(T instance) where T : class;
}
```

## Behavioral Contract

### `Get<T>()`

- Returns the registered instance for type `T`, or `null` if not registered.
- Type parameter must be an interface type (by convention, not enforced).
- Thread-safe: backed by `ConcurrentDictionary`.

### `Set<T>(T instance)`

- Registers or overwrites the feature for type `T`.
- Throws `ArgumentNullException` if `instance` is null.
- Thread-safe.

### Enumeration

- Iterates all registered features as `KeyValuePair<Type, object>`.
- Useful for diagnostic or discovery scenarios.

## Pattern Usage

All provider clients implement `IHasFeatures` and populate their `FeatureCollection` in the constructor:

```csharp
public OpenAiChatCompletionClient(...)
{
    var features = new FeatureCollection();
    features.Set<IJsonOutputFeature>(this);
    features.Set<IToolCallingFeature>(this);
    features.Set<IStreamingChatFeature>(this);
    Features = features;
}
```

Consumers discover features at runtime:

```csharp
if (client.Features.Get<IStreamingChatFeature>() is { } streaming)
{
    // Provider supports streaming
    await foreach (var chunk in streaming.GetChatCompletionStreamAsync(request))
        Console.Write(chunk.Content);
}
else
{
    // Fallback to non-streaming
    var response = await client.GetChatCompletionAsync(request);
}
```

## Design Rationale

Inspired by ASP.NET Core's `HttpContext.Features`. This pattern allows:
- Adding new capabilities without breaking the `IChatCompletionClient` interface.
- Provider-specific features (e.g., `IGroundedChatFeature` for Cohere only).
- Compile-time safety through interface types as dictionary keys.
- Graceful degradation when a provider doesn't support a feature.
