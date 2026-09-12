# Embeddings

Cisharpai provides a unified `IEmbeddingClient` interface for generating text embeddings across multiple providers. Currently **OpenAI**, **Azure OpenAI**, **Azure AI Inference**, and **Cohere** are supported. See the [Provider Feature Matrix](provider-features.md) for a full capability overview.

## Core concepts

All embedding operations use the shared `EmbeddingRequest` and `EmbeddingResponse` models from `Cisharpai.Models`. This means you can swap providers without changing your application code.

### EmbeddingRequest

```csharp
var request = new EmbeddingRequest(
    Input: ["Text to embed"],           // one or more strings
    Model: "text-embedding-3-small",    // provider model name
    InputType: EmbeddingInputType.Query, // optional hint
    Dimensions: 256,                     // optional dimension reduction
    EncodingFormat: null,                // "float" (default) or "base64"
    IncludeRawResponse: false,           // include raw JSON for debugging
    ExtraParameters: null);              // passthrough JSON for provider-specific options
```

### EmbeddingResponse

```csharp
response.Embeddings      // IReadOnlyList<float[]> - the embedding vectors
response.Dimensions       // dimension of each vector
response.Model            // actual model used
response.TotalTokens      // token usage
response.IsSuccess        // success indicator
response.ErrorMessage     // error details if failed
```

## OpenAI embeddings

### Setup

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
```

### Single text

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"],
    Model: "text-embedding-3-small"));

Console.WriteLine($"Dimensions: {response.Dimensions}");
Console.WriteLine($"First value: {response.Embeddings[0][0]}");
```

### Batch input

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["First text", "Second text", "Third text"],
    Model: "text-embedding-3-small"));

// response.Embeddings[0], [1], [2] contain the vectors
```

### Custom dimensions

OpenAI `text-embedding-3-*` models support dimension reduction:

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"],
    Model: "text-embedding-3-small",
    Dimensions: 256));
// response.Embeddings[0].Length == 256
```

### Supported models

- `text-embedding-3-small` (1536 dimensions by default)
- `text-embedding-3-large` (3072 dimensions by default)

## Azure OpenAI embeddings

### Setup

```csharp
using Cisharpai;
using Cisharpai.Azure.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddAzureOpenAiEmbeddingClient(options =>
{
    options.Endpoint = "https://myresource.openai.azure.com";
    options.ApiKey = "YOUR_AZURE_OPENAI_KEY";
    options.DeploymentName = "text-embedding-3-small";
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IEmbeddingClient>();
```

### Usage

Azure OpenAI embeddings use deployment-based routing. The model is determined by your deployment, so you do not need to specify `Model` in the request.

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"]));

Console.WriteLine($"Dimensions: {response.Dimensions}");
```

### Custom dimensions

Azure OpenAI `text-embedding-3-*` deployments support dimension reduction, just like the standard OpenAI API:

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"],
    Dimensions: 256));
```

### Supported deployments

- `text-embedding-ada-002` (1536 dimensions)
- `text-embedding-3-small` (1536 dimensions by default)
- `text-embedding-3-large` (3072 dimensions by default)

## Azure AI Inference embeddings

### Setup

```csharp
using Cisharpai;
using Cisharpai.Azure.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddAzureAiInferenceEmbeddings(options =>
{
    options.Endpoint = "https://mymodel.eastus.models.ai.azure.com";
    options.ApiKey = "YOUR_AZURE_INFERENCE_KEY";
    options.ModelId = "your-embedding-model-id";
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IEmbeddingClient>();
```

### Text embeddings

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"],
    Model: "your-embedding-model-id"));

Console.WriteLine($"Dimensions: {response.Dimensions}");
```

### Image embeddings

Azure AI Inference also supports image embeddings via the `IImageEmbeddingFeature` feature:

```csharp
if (client.Features.Get<IImageEmbeddingFeature>() is { } imageFeature)
{
    var response = await imageFeature.GetImageEmbeddingAsync(
        imagePath: "path/to/image.png",
        model: "your-embedding-model-id");

    Console.WriteLine($"Dimensions: {response.Dimensions}");
}
```

### Supported models

Available models depend on your Azure AI Inference deployment. Common embedding models include those available through the Azure AI model catalog.

## Cohere embeddings

### Setup

```csharp
using Cisharpai;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddCohereEmbeddingClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IEmbeddingClient>();
```

### Usage

Cohere requires an `InputType` for all requests. If not specified, it defaults to `Document`.

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Search this document text"],
    Model: "embed-english-v3.0",
    InputType: EmbeddingInputType.Document));
```

### Input types

The `EmbeddingInputType` enum maps to Cohere's `input_type` parameter:

| EmbeddingInputType | Cohere value | Use case |
|---|---|---|
| `Query` | `search_query` | Text used as a search query |
| `Document` | `search_document` | Text stored in a search index |
| `Classification` | `classification` | Text for classification |
| `Clustering` | `clustering` | Text for clustering |

### Supported models

- `embed-english-v3.0` (1024 dimensions)
- `embed-multilingual-v3.0` (1024 dimensions)

## Error handling

Embedding clients never throw exceptions. Errors are returned via the response:

```csharp
var response = await client.GetEmbeddingsAsync(request);
if (!response.IsSuccess)
{
    Console.WriteLine($"Error: {response.ErrorMessage}");
    // response.RawResponseJson may contain the provider error body
}
```

## Debugging

Set `IncludeRawResponse: true` to inspect the raw JSON sent to and received from the provider:

```csharp
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["test"],
    Model: "text-embedding-3-small",
    IncludeRawResponse: true));

Console.WriteLine(response.RawRequestJson);
Console.WriteLine(response.RawResponseJson);
```
