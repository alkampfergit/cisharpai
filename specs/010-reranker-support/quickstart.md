# Quickstart: Reranking

## Register the client

```csharp
services.AddCohereRerankerClient(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY")!;
    options.DefaultModel = CohereModels.Rerank.RerankV3_5;
});
```

## Rerank documents

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

`Results` comes back most-relevant first, and `Index` points into the `documents` array you
passed in.

## Point at an Azure-hosted (or any other) deployment

```csharp
services.AddCohereRerankerClient(options =>
{
    options.ApiKey = "...";
    options.BaseUrl = "https://my-cohere-deployment.inference.ai.azure.com/v2/";
    options.DefaultModel = "rerank-v3.5";
});
```

## Use Cohere's `priority` (or any other provider parameter)

```csharp
var response = await client.RerankAsync(new RerankRequest(
    Query: query,
    Documents: documents,
    ExtraParameters: JsonSerializer.SerializeToElement(new { priority = 500 })));
```

## Keyed registration

```csharp
services.AddCohereRerankerClient("tenant-a", o => { o.ApiKey = keyA; });
services.AddCohereRerankerClient("tenant-b", o => { o.ApiKey = keyB; });

var clientA = provider.GetRequiredKeyedService<IRerankerClient>("tenant-a");
```

## Runtime provider selection via the factory

```csharp
services.AddCisharpaiClientFactory().AddCohereSupport();

var result = factory.CreateRerankerClient(new CohereClientConfiguration
{
    ApiKey = apiKey,
    DefaultModel = CohereModels.Rerank.RerankV3_5
});

if (result.IsSuccess)
{
    var rerankClient = result.Client!;
}
```

## Unit-test without HTTP

```csharp
var fake = new FakeRerankerClient();
fake.EnqueueResponse(FakeResponses.Rerank((1, 0.99), (0, 0.42)));

var response = await fake.RerankAsync(new RerankRequest("q", ["a", "b"]));

Assert.That(response.Results[0].Index, Is.EqualTo(1));
Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
```
