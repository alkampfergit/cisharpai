# Contract: IEmbeddingClient

**File**: `src/Cisharpai/IEmbeddingClient.cs`

## Interface Definition

```csharp
public interface IEmbeddingClient : IHasFeatures
{
    Task<EmbeddingResponse> GetEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
```

## Behavior

### `GetEmbeddingsAsync`

Generates vector embeddings from one or more text inputs.

**Input**: `EmbeddingRequest` — immutable record containing text inputs, optional model, dimensions, encoding format, input type, extra parameters, and raw response flag.

**Output**: `EmbeddingResponse` — immutable record containing float vectors (ordered by input index), model name, token usage, dimensions, success/error state, and optional raw JSON.

**Error behavior**:
- HTTP errors (4xx, 5xx): Returns `EmbeddingResponse` with `IsSuccess=false`, `ErrorMessage` containing status code, `RawResponseJson` containing the error body (when available).
- No model specified and no default configured: Throws `InvalidOperationException`.
- Network failures: Caught and returned as `IsSuccess=false` with exception message.

**Model resolution order**:
1. `request.Model`
2. Provider options `DefaultModel`
3. Throw `InvalidOperationException`

### Feature Collection (`IHasFeatures`)

The `Features` property exposes optional embedding capabilities:
- `IImageEmbeddingFeature` — available on Azure AI Inference and Cohere
- `IMultimodalEmbeddingFeature` — available on Cohere only

Consumers discover features via `client.Features.Get<T>()`.

## Providers

| Provider | Class | Project |
|----------|-------|---------|
| OpenAI | `OpenAiEmbeddingClient` | `Cisharpai.OpenAi` |
| Azure OpenAI | `AzureOpenAiEmbeddingClient` | `Cisharpai.Azure` |
| Azure AI Inference | `AzureAiInferenceEmbeddingClient` | `Cisharpai.Azure` |
| Cohere | `CohereEmbeddingClient` | `Cisharpai.Cohere` |
| Fake (testing) | `FakeEmbeddingClient` | `Cisharpai.Testing` |

## Usage Example

```csharp
IEmbeddingClient client = /* any provider */;

var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"],
    Model: "text-embedding-3-small",
    Dimensions: 256));

if (response.IsSuccess)
{
    float[] vector = response.Embeddings[0];
}
```
