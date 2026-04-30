# Cohere Provider

## Setup

```csharp
services.AddCohereClient(options =>
{
    options.ApiKey = "...";
    options.BaseUrl = "https://api.cohere.com/v2";  // default
    options.DefaultModel = "command-a-08-2025";
});
```

**Options:**
- `ApiKey` (required)
- `BaseUrl` — Override for proxies
- `DefaultModel` — Fallback model

## Available Models (`CohereModels`)

**Chat:**
- `CohereModels.Chat.CommandA` — `command-a-08-2025`
- `CohereModels.Chat.CommandRPlus` — `command-r-plus-08-2024`
- `CohereModels.Chat.CommandR` — `command-r-08-2024`

**Embedding:**
- `CohereModels.Embedding.EmbedV4` — `embed-v4.0`
- `CohereModels.Embedding.EmbedEnglishV3` — `embed-english-v3.0`
- `CohereModels.Embedding.EmbedMultilingualV3` — `embed-multilingual-v3.0`

## Supported Features

- `IJsonOutputFeature` — JSON Mode + Structured Outputs
- `IToolCallingFeature` — With uppercase ToolChoice values
- `IStreamingChatFeature` — Event-based SSE
- `IGroundedChatFeature` — RAG with document citations (Cohere only)
- `IImageEmbeddingFeature` — Single image embedding
- `IMultimodalEmbeddingFeature` — Mixed text + image (Embed v4)
- Vision — Partial (image parts silently skipped, only text extracted)

## Grounded Chat (RAG)

See [grounded-chat.md](grounded-chat.md) for full details.

```csharp
var groundedFeature = client.Features.Get<IGroundedChatFeature>();
var response = await groundedFeature.GetGroundedChatCompletionAsync(request,
    new GroundedChatOptions
    {
        Documents =
        [
            new DocumentChunk("doc-1", new Dictionary<string, string>
            {
                ["title"] = "Company Policy",
                ["snippet"] = "All employees must..."
            })
        ],
        CitationMode = CitationMode.Accurate
    });

foreach (var citation in response.Citations)
    Console.WriteLine($"[{citation.Start}-{citation.End}] {citation.Text}");
```

## Embedding Quirks

**InputType is required** for Cohere v3 embeddings:
- `search_query` — For queries
- `search_document` — For documents to search
- `classification` — For classification
- `clustering` — For clustering

**Image format:** Data URIs required (`data:image/png;base64,...`). Raw base64 returns 422.

**Minimum image size:** At least 64x64 pixels. Tiny images (1x1) fail decoding.

**Mutually exclusive fields:** `images`, `inputs`, and `texts` cannot be combined.

## Tool Calling Quirks

- ToolChoice values are uppercase: `AUTO`, `NONE`, `REQUIRED`
- `ToolChoice.Specific("name")` degrades to `REQUIRED` (Cohere doesn't support named tool choice)
- Parameters use snake_case in the API

## Streaming

Event-based SSE with events:
- `content-delta` — Token content
- `message-end` — Stream complete with uppercase `finish_reason`

## Vision Limitation

Image parts in messages are **silently skipped** — only text content is extracted. Use embeddings for image processing with Cohere.
