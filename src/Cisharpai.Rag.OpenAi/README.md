# Cisharpai.Rag.OpenAi

Bridge package that adds OpenAI hosted retrieval (`file_search` via the Responses API) to Cisharpai. Install this **alongside** `Cisharpai.OpenAi`, not instead of it.

```csharp
using Cisharpai.OpenAi;
using Cisharpai.Rag;
using Cisharpai.Rag.OpenAi;

// Register both the OpenAI chat client and hosted retrieval:
services.AddOpenAiClient(opt =>
{
    opt.ApiKey = "sk-...";
    opt.DefaultModel = "gpt-4o";
});

services.AddOpenAiHostedRetrieval(opt =>
{
    opt.ApiKey = "sk-...";
    opt.DefaultModel = "gpt-4o";
});
```

Once registered, discover the feature through the standard `Features.Get<T>()` pattern and obtain an `IRetriever` bound to a specific vector store:

```csharp
var feature = chatClient.Features.Get<IHostedRetrievalFeature>()!;
IRetriever retriever = feature.ForStore("vs_my_store");

IReadOnlyList<ScoredChunk> results = await retriever.RetrieveAsync("search query", topK: 5);
```

Each `ForStore` call returns an independent, thread-safe `IRetriever`. Consumers that need only retrieval depend on `IRetriever` directly and can swap implementations (hosted, BM25, hybrid) by changing a DI registration.

This package references both `Cisharpai.OpenAi` and `Cisharpai.Rag`, keeping the OpenAI provider package free of any RAG dependency for users who do not need retrieval.

See the [RAG usage guide](https://github.com/alkampfergit/cisharpai/blob/main/wiki/rag.md) for vector store management, file upload, and configuration details.
