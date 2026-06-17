# Cisharpai — Frequently Asked Questions

This document is a living collection of questions and answers about using the Cisharpai library.
It is automatically maintained and updated as new questions are answered.

---

## Q: How do I construct a `CisharpaiClientFactoryResult<IEmbeddingClient>` in test code?
**Date**: 2026-06-17

`CisharpaiClientFactoryResult<T>` has a **private constructor** and **`private init` properties**. The only way to construct it is via two static factory methods:

```csharp
// Success — wraps a client instance
CisharpaiClientFactoryResult<IEmbeddingClient>.Success(embeddingClient)

// Failure — wraps an error message
CisharpaiClientFactoryResult<IEmbeddingClient>.Failure("Provider not registered")
```

### Recommended approach: use `Cisharpai.Testing`

Instead of mocking `ICisharpaiClientFactory` with NSubstitute, prefer the built-in `FakeClientFactoryProvider` which handles result construction internally:

```csharp
var fakeEmbedding = new FakeEmbeddingClient
{
    DefaultResponse = FakeResponses.Embedding()
};

var fakeProvider = new FakeClientFactoryProvider()
    .WithDefaultEmbeddingClient(fakeEmbedding);

services.AddCisharpaiClientFactory()
    .AddFakeSupport(fakeProvider);
```

`FakeClientFactoryProvider` wraps the client in `CisharpaiClientFactoryResult<T>.Success(...)` for you — you never touch the result type directly. It also supports queued clients via `.EnqueueEmbeddingClient()`.

### Alternative: mock `ICisharpaiClientFactory` directly

When you need full control (e.g., simulating factory failure):

```csharp
var fakeEmbedding = new FakeEmbeddingClient();
var factory = Substitute.For<ICisharpaiClientFactory>();

// Success
factory.CreateEmbeddingClient(Arg.Any<CisharpaiClientConfiguration>())
    .Returns(CisharpaiClientFactoryResult<IEmbeddingClient>.Success(fakeEmbedding));

// Failure
factory.CreateEmbeddingClient(Arg.Any<CisharpaiClientConfiguration>())
    .Returns(CisharpaiClientFactoryResult<IEmbeddingClient>.Failure("Provider not registered"));
```

**Important**: `CreateEmbeddingClient` accepts a `CisharpaiClientConfiguration` parameter, not a `string`.

**Source files**: `src/Cisharpai/CisharpaiClientFactoryResult.cs`, `src/Cisharpai.Testing/FakeClientFactoryProvider.cs`

---
