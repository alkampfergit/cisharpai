# Reranking

Reranking reorders a candidate document set by true relevance to a query. It is the second stage
of a RAG pipeline: vector search retrieves a broad candidate set cheaply, then a reranker model —
which reads the query and each document together — picks the handful that actually answer it.

**Provider support: Cohere only.** OpenAI, Azure OpenAI, Azure AI Inference, and Anthropic have
no rerank API.

Reranking is **not** a feature interface. It is its own top-level client, `IRerankerClient`,
alongside `IChatCompletionClient` and `IEmbeddingClient`.

## Setup

```csharp
services.AddCohereRerankerClient(options =>
{
    options.ApiKey = "...";
    options.DefaultModel = CohereModels.Rerank.RerankV3_5;
});
```

Keyed overload for multi-tenant scenarios:

```csharp
services.AddCohereRerankerClient("tenant-a", o => { o.ApiKey = keyA; });
var clientA = provider.GetRequiredKeyedService<IRerankerClient>("tenant-a");
```

## Models (`CohereModels.Rerank`)

- `RerankV3_5` — `rerank-v3.5`
- `RerankEnglishV3` — `rerank-english-v3.0`
- `RerankMultilingualV3` — `rerank-multilingual-v3.0`

## Basic Usage

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
    logger.LogWarning("Rerank failed: {Error}", response.ErrorMessage);
    return;
}

foreach (var result in response.Results)
{
    Console.WriteLine($"{result.RelevanceScore:F4}  {documents[result.Index]}");
}
```

## DTOs

**RerankRequest:** `Query`, `Documents` (string[]), `Model?`, `TopN?`, `MaxTokensPerDocument?`,
`IncludeRawResponse`, `ExtraParameters?`

**RerankResponse:** `Results`, `Model`, `SearchUnits?`, `InputTokens?`, `RawResponseJson`,
`RawRequestJson`, `IsSuccess`, `ErrorMessage`

**RerankResult:** `Index`, `RelevanceScore`

## Key Gotchas

**`Index` is into YOUR array.** The response carries indices, not document text — index back into
the `Documents` list you passed in to recover the content. This is why the request documents must
stay alive for as long as you need the results.

**Results are already ordered.** Most relevant first, as returned by the provider. Do not re-sort.

**Scores are provider-defined.** Compare `RelevanceScore` values within a single response, never
across providers or models. There is no absolute "good score" threshold that transfers.

**Missing model throws.** If neither `RerankRequest.Model` nor `CohereClientOptions.DefaultModel`
is set, `RerankAsync` throws `InvalidOperationException`. That is a configuration error, not an
API error — API errors still return `IsSuccess=false`.

**`TopN` caps the results, not the input.** All documents are sent and scored; `TopN` only limits
how many come back.

## Alternative Hosting (Azure AI Foundry, self-hosted)

Cohere rerank models on Azure AI Foundry expose Cohere's own rerank contract, not a unified Azure
endpoint — so there is no separate Azure reranker provider. Retarget the Cohere client:

```csharp
services.AddCohereRerankerClient(options =>
{
    options.ApiKey = "...";
    options.BaseUrl = "https://my-cohere-deployment.inference.ai.azure.com/v2/";
    options.DefaultModel = "rerank-v3.5";
});
```

The `rerank` path is appended relative to `BaseUrl`; request shape, response mapping, and error
handling are unchanged.

## Cohere `priority`

Cohere's `priority` scheduling hint is deliberately absent from `RerankRequest` — it has no
analogue at other providers and would leak a provider concept into the unified abstraction. Use
the escape hatch:

```csharp
ExtraParameters: JsonSerializer.SerializeToElement(new { priority = "high" })
```

The same mechanism reaches any future Cohere rerank parameter without a library update.

## Runtime Creation via Factory

```csharp
services.AddCisharpaiClientFactory().AddCohereSupport();

var result = factory.CreateRerankerClient(new CohereClientConfiguration
{
    ApiKey = apiKey,
    DefaultModel = CohereModels.Rerank.RerankV3_5
});

// Non-Cohere providers: IsSuccess=false,
// "Provider 'OpenAi' does not support reranker clients."
```

## Testing

```csharp
var fake = new FakeRerankerClient
{
    DefaultResponse = FakeResponses.Rerank((1, 0.99), (0, 0.42))
};

var response = await fake.RerankAsync(new RerankRequest("q", documents));

Assert.That(response.Results[0].Index, Is.EqualTo(1));
Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
```

Also available: `FakeResponses.Rerank(documentCount)`, `FakeResponses.RerankError(message)`,
`services.AddFakeRerankerClient()`, and `EnqueueResponse` / `Reset` on the fake.
