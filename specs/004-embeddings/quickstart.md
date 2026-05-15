# Quickstart: Embeddings

## Prerequisites

- .NET 8.0 or .NET 10 SDK
- An API key for at least one supported provider (OpenAI, Azure OpenAI, Azure AI Inference, or Cohere)

## Install

```bash
dotnet add package Cisharpai
dotnet add package Cisharpai.OpenAi     # or Cisharpai.Azure, Cisharpai.Cohere
```

## 1. Text Embeddings (any provider)

```csharp
using Cisharpai;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddOpenAiEmbeddingClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IEmbeddingClient>();

var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"],
    Model: "text-embedding-3-small"));

Console.WriteLine($"Dimensions: {response.Dimensions}");
Console.WriteLine($"First value: {response.Embeddings[0][0]}");
```

## 2. Batch Embeddings

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["First text", "Second text", "Third text"],
    Model: "text-embedding-3-small"));

// response.Embeddings[0], [1], [2] contain the vectors
```

## 3. Custom Dimensions

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"],
    Model: "text-embedding-3-small",
    Dimensions: 256));
// response.Embeddings[0].Length == 256
```

## 4. Image Embeddings (Azure AI Inference / Cohere)

```csharp
using Cisharpai.Features.Embeddings;

if (client.Features.Get<IImageEmbeddingFeature>() is { } imageFeature)
{
    var response = await imageFeature.GetImageEmbeddingAsync(
        imagePath: "path/to/image.png",
        model: "your-embedding-model");

    Console.WriteLine($"Image dimensions: {response.Dimensions}");
}
```

## 5. Multimodal Embeddings (Cohere Embed v4)

```csharp
using Cisharpai.Features.Embeddings;
using Cisharpai.Models;

if (client.Features.Get<IMultimodalEmbeddingFeature>() is { } multimodal)
{
    var inputs = new List<MultimodalEmbeddingInput>
    {
        new([
            new TextEmbeddingContent("A chart showing Q3 growth"),
            new ImageEmbeddingContent("path/to/chart.png")
        ])
    };

    var response = await multimodal.GetMultimodalEmbeddingsAsync(
        inputs, "embed-v4.0",
        outputDimension: 256);

    Console.WriteLine($"Dimensions: {response.Dimensions}");
}
```

## 6. Error Handling

```csharp
var response = await client.GetEmbeddingsAsync(request);
if (!response.IsSuccess)
{
    Console.WriteLine($"Error: {response.ErrorMessage}");
}
```

## 7. Debugging with Raw JSON

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["test"],
    Model: "text-embedding-3-small",
    IncludeRawResponse: true));

Console.WriteLine(response.RawRequestJson);
Console.WriteLine(response.RawResponseJson);
```

## 8. Unit Testing with Fakes

```csharp
using Cisharpai.Testing;

var fake = new FakeEmbeddingClient();
fake.DefaultResponse = FakeResponses.Embedding();

var response = await fake.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["test"],
    Model: "text-embedding-3-small"));

Assert.That(response.IsSuccess, Is.True);
Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
```

## Verification

Run the unit tests to verify the embedding feature:

```bash
dotnet test src/Cisharpai.Tests/ --filter "FullyQualifiedName~Embedding"
```
