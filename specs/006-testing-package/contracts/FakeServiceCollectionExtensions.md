# Contract: FakeServiceCollectionExtensions

**File**: `src/Cisharpai.Testing/FakeServiceCollectionExtensions.cs`

## Class Definition

```csharp
public static class FakeServiceCollectionExtensions
```

## Behavior

DI registration helpers that create fake client instances, register them as singletons, and return the instance for test setup and assertions.

### `AddFakeChatCompletionClient(enabledFeatures?)`

```csharp
public static FakeChatCompletionClient AddFakeChatCompletionClient(
    this IServiceCollection services,
    FakeChatFeatures enabledFeatures = FakeChatFeatures.All)
```

1. Creates a `FakeChatCompletionClient` with the specified features.
2. Registers it as `IChatCompletionClient` singleton.
3. Returns the concrete `FakeChatCompletionClient` instance.

The returned instance can be used for:
- Setting `DefaultResponse` / `EnqueueResponse()` before the test
- Inspecting `ReceivedRequests` / `CallCount` after the test

### `AddFakeEmbeddingClient(enabledFeatures?)`

```csharp
public static FakeEmbeddingClient AddFakeEmbeddingClient(
    this IServiceCollection services,
    FakeEmbeddingFeatures enabledFeatures = FakeEmbeddingFeatures.All)
```

1. Creates a `FakeEmbeddingClient` with the specified features.
2. Registers it as `IEmbeddingClient` singleton.
3. Returns the concrete `FakeEmbeddingClient` instance.

## Usage Pattern

```csharp
var services = new ServiceCollection();

// Register and get reference in one call
var fake = services.AddFakeChatCompletionClient();
fake.DefaultResponse = FakeResponses.Chat("mocked");

var provider = services.BuildServiceProvider();

// Application code resolves IChatCompletionClient — gets the fake
var myService = ActivatorUtilities.CreateInstance<MyService>(provider);
await myService.DoWorkAsync();

// Assert on what was sent
Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
```
