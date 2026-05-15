# Contract: LlmHttpClient — SerializeAndMerge & Raw JSON Capture

**File**: `src/Cisharpai/LlmHttpClient.cs`

## SerializeAndMerge (private)

```csharp
private string SerializeAndMerge<TRequest>(TRequest payload, JsonElement? extraParameters)
```

1. Serializes `payload` to JSON using `System.Text.Json` with `camelCase` naming and `WhenWritingNull` ignore.
2. If `extraParameters` has a value and `ValueKind == Object`, calls `JsonDeepMerge.Merge(json, extraParameters.Value)`.
3. Returns the final JSON string (merged or original).

**Called by**: `PostAsync`, `PostWithRawAsync`, `PostStreamAsync` — all three HTTP methods pass provider request DTOs and `extraParameters` through this method.

## PostAsync (standard requests)

```csharp
public async Task<TResponse> PostAsync<TRequest, TResponse>(
    string uri,
    TRequest payload,
    JsonElement? extraParameters = null,
    CancellationToken cancellationToken = default)
```

Serializes, merges, sends HTTP POST, deserializes response. Does NOT capture raw JSON.

## PostWithRawAsync (raw JSON capture)

```csharp
public async Task<(TResponse Result, string RawResponseJson, string RawRequestJson)> PostWithRawAsync<TRequest, TResponse>(
    string uri,
    TRequest payload,
    JsonElement? extraParameters = null,
    CancellationToken cancellationToken = default)
```

Same as `PostAsync` but returns a tuple including:
- `RawResponseJson`: The exact response body string from the provider
- `RawRequestJson`: The post-merge JSON string that was sent

**Used when**: Provider clients detect `IncludeRawResponse == true` on the request and call `PostWithRawAsync` instead of `PostAsync`.

## PostStreamAsync (streaming with merge)

```csharp
public async IAsyncEnumerable<string> PostStreamAsync<TRequest>(
    string uri,
    TRequest payload,
    JsonElement? extraParameters = null,
    CancellationToken cancellationToken = default)
```

Serializes, merges, sends HTTP POST, yields raw SSE data lines. ExtraParameters are merged into the streaming request body.

## Provider Integration Pattern

Each provider client follows this pattern:

```csharp
// In provider's GetChatCompletionAsync:
if (request.IncludeRawResponse)
{
    var (result, rawResponse, rawRequest) = await _httpClient.PostWithRawAsync<ProviderRequest, ProviderResponse>(
        uri, providerRequest, request.ExtraParameters, cancellationToken);
    return MapToUnifiedResponse(result, rawResponse, rawRequest);
}
else
{
    var result = await _httpClient.PostAsync<ProviderRequest, ProviderResponse>(
        uri, providerRequest, request.ExtraParameters, cancellationToken);
    return MapToUnifiedResponse(result);
}
```
