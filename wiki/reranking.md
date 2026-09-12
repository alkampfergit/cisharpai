# Reranking

Reranking reorders a candidate set of documents by their true relevance to a query. It is the
second stage of a typical RAG pipeline: a vector search retrieves a broad candidate set cheaply,
and a reranker model — which reads the query and each document together — picks the handful that
actually answer the question.

Cisharpai exposes this through `IRerankerClient`, the same shape as `IChatCompletionClient` and
`IEmbeddingClient`.

| Provider | Reranking |
|----------|-----------|
| Cohere | Yes |
| OpenAI / Azure OpenAI / Azure AI Inference / Anthropic | -- |

> Cohere rerank models deployed on Azure AI Foundry expose Cohere's own rerank contract rather
> than a unified Azure endpoint. Point `BaseUrl` at the Azure deployment and use the Cohere
> client — see [Alternative hosting](#alternative-hosting).

## Registration

```csharp
services.AddCohereRerankerClient(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY")!;
    options.DefaultModel = CohereModels.Rerank.RerankV3_5;
});
```

Well-known models on `CohereModels.Rerank`:

| Constant | Identifier |
|----------|-----------|
| `RerankV3_5` | `rerank-v3.5` |
| `RerankEnglishV3` | `rerank-english-v3.0` |
| `RerankMultilingualV3` | `rerank-multilingual-v3.0` |

## Reranking documents

```csharp
var client = provider.GetRequiredService<IRerankerClient>();

string[] documents =
[
    "Carson City is the capital city of Nevada.",
    "Paris is the capital and most populous city of France.",
    "The Louvre is a museum in Paris."
];

var response = await client.RerankAsync(new RerankRequest(
    Query: "What is the capital of France?",
    Documents: documents,
    TopN: 2));

if (!response.IsSuccess)
{
    Console.WriteLine($"Rerank failed: {response.ErrorMessage}");
    return;
}

foreach (var result in response.Results)
{
    Console.WriteLine($"{result.RelevanceScore:F4}  {documents[result.Index]}");
}
```

### `RerankRequest`

| Property | Meaning |
|----------|---------|
| `Query` | The query the documents are ranked against. |
| `Documents` | Candidate documents, in your own order. |
| `Model` | Model identifier. Falls back to `DefaultModel` in options. |
| `TopN` | Return only the top N results. `null` returns all. |
| `MaxTokensPerDocument` | Per-document truncation budget. |
| `IncludeRawResponse` | Populates `RawRequestJson` / `RawResponseJson`. |
| `ExtraParameters` | Deep-merged into the request body. |

### `RerankResponse`

| Property | Meaning |
|----------|---------|
| `Results` | Ranked results, most relevant first. |
| `Model` | The model that produced the ranking. |
| `SearchUnits` / `InputTokens` | Billing counters, when the provider reports them. |
| `RawResponseJson` / `RawRequestJson` | Wire payloads when `IncludeRawResponse` is set. |
| `IsSuccess` / `ErrorMessage` | Error state — API errors never throw. |

### `RerankResult`

`Index` is the zero-based position of the document in the `Documents` list **you** passed in, so
you index back into your own collection to recover the text. `RelevanceScore` is the provider's
score; scales are provider-defined, so compare within a single response rather than across
providers or models.

## Alternative hosting

`BaseUrl` is fully caller-controlled, so an Azure AI Foundry deployment (or any other host) is a
one-setting change:

```csharp
services.AddCohereRerankerClient(options =>
{
    options.ApiKey = "...";
    options.BaseUrl = "https://my-cohere-deployment.inference.ai.azure.com/v2/";
    options.DefaultModel = "rerank-v3.5";
});
```

The `rerank` path is appended relative to `BaseUrl`; everything else is unchanged.

## Provider-specific parameters

Cohere's `priority` scheduling hint is deliberately not part of the unified `RerankRequest` — it
has no analogue at other providers. Use `ExtraParameters`, which deep-merges into the request
body:

```csharp
var response = await client.RerankAsync(new RerankRequest(
    Query: query,
    Documents: documents,
    ExtraParameters: JsonSerializer.SerializeToElement(new { priority = "high" })));
```

The same mechanism reaches any future Cohere rerank parameter without waiting for a library
update.

## Keyed registration

For multi-tenant setups, register per key:

```csharp
services.AddCohereRerankerClient("tenant-a", o => { o.ApiKey = keyA; });
services.AddCohereRerankerClient("tenant-b", o => { o.ApiKey = keyB; });

var clientA = provider.GetRequiredKeyedService<IRerankerClient>("tenant-a");
```

## Runtime creation via the client factory

```csharp
services.AddCisharpaiClientFactory().AddCohereSupport();

var result = factory.CreateRerankerClient(new CohereClientConfiguration
{
    ApiKey = apiKey,
    DefaultModel = CohereModels.Rerank.RerankV3_5
});

if (!result.IsSuccess)
{
    // e.g. "Provider 'OpenAi' does not support reranker clients."
    Console.WriteLine(result.ErrorMessage);
    return;
}

var rerankClient = result.Client!;
```

Providers without rerank support return a failure result rather than throwing. See
[Client Factory](factory.md).

## Error handling

API-level failures follow the library-wide convention — no exceptions:

```csharp
var response = await client.RerankAsync(request);
if (!response.IsSuccess)
{
    logger.LogWarning("Rerank failed: {Error}", response.ErrorMessage);
}
```

The one exception is configuration: if neither `RerankRequest.Model` nor
`CohereClientOptions.DefaultModel` is set, `RerankAsync` throws `InvalidOperationException`.

## Testing

Use `FakeRerankerClient` from `Cisharpai.Testing` — see [Testing](testing.md#fakererankerclient).
