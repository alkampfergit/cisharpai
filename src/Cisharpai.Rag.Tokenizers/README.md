# Cisharpai.Rag.Tokenizers

Local token counting for Cisharpai RAG pipelines via `Microsoft.ML.Tokenizers`. Replaces the default `s.Length / 4` heuristic in `BulkEmbeddingOptions.TokenEstimator` with real tokenizer counts for OpenAI-compatible models.

```csharp
using Cisharpai.Rag.Tokenization;

// Offline, synchronous, thread-safe — cached tokenizer instance per counter.
var counter = new TiktokenCounter("gpt-4o"); // o200k_base
int tokens = counter.CountTokens("Hello, world!");

// Wire into bulk embedding options:
var options = new BulkEmbeddingOptions
{
    MaxBatchTokens = 8000,
    TokenEstimator = counter.ToTokenEstimator() // replaces s.Length / 4
};
```

Supports `gpt-4o` (o200k_base) and `gpt-4` / `gpt-3.5-turbo` (cl100k_base). Ships the `Microsoft.ML.Tokenizers.Data.O200kBase` and `Cl100kBase` vocabulary packages.

`ToTokenEstimator()` is defined on the concrete `TiktokenCounter` type (not `ITokenCounter`) so that remote async counters cannot be accidentally used in the synchronous batching loop.

This package is opt-in — `Cisharpai.Rag` does not depend on it, so consumers who only need bulk embeddings with the approximate heuristic avoid restoring the multi-megabyte tokenizer data files.

See the [RAG usage guide](https://github.com/alkampfergit/cisharpai/blob/main/wiki/rag.md#token-counting) for full documentation.
