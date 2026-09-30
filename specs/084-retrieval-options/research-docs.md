# Documentation Research

## Topics Researched
- .NET RAG retrieval options and metadata filtering patterns
- Semantic Kernel ITextSearch / TextSearchOptions / TextSearchFilter design
- Provider-specific query extension patterns in .NET search abstractions

## Findings

### Semantic Kernel TextSearchOptions Pattern

**Source**: [Text Search with Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/concepts/text-search/)
**Relevance**: Semantic Kernel's `TextSearchOptions` + `TextSearchFilter` is the closest .NET ecosystem precedent for portable retrieval options with metadata equality filtering.

- `TextSearchFilter` uses an `Equality(key, value)` builder pattern for portable AND-combined metadata filters
- `TextSearchOptions` wraps the filter alongside `Top` (equivalent to TopK) and `Skip`
- The filter is optional; searches work with or without it
- Recent modernisation (PR #13179) replaced the internal `VectorSearchFilter` with LINQ-based filtering, keeping the public API stable

### Semantic Kernel ITextSearch Evolution

**Source**: [ITextSearch filtering modernisation](https://github.com/microsoft/semantic-kernel/issues/10456)
**Relevance**: Shows how a portable search interface evolves to support provider-specific extensions without breaking the common contract.

- The common interface accepts `TextSearchOptions` (portable)
- Provider-specific connectors (Azure AI Search, Qdrant, etc.) accept additional parameters through their own option types
- The pattern validates that keeping the common surface small (equality-only) while deferring rich queries to provider types is a proven approach in the .NET ecosystem

### RAG Metadata Filtering Best Practices

**Source**: [Multi-Meta-RAG](https://arxiv.org/pdf/2406.13213)
**Relevance**: Academic validation that metadata-based pre-filtering improves retrieval precision and latency.

- Metadata filtering scopes retrieval to a relevant portion of the knowledge base before semantic ranking
- AND-combined equality filters are the most common portable filter type across vector stores
- Richer operators (range, contains, OR) are provider-specific and should not be in the common contract

---

## Summary

The Semantic Kernel `TextSearchOptions` / `TextSearchFilter` pattern validates the design proposed in issue #84: a small common surface (top-K, score threshold, AND-equality metadata filter) with an optional provider-specific extension type. Keeping the common filter to equality-only AND combinations is consistent with both the Semantic Kernel approach and academic best practices for portable RAG filtering. Provider-specific query extensions (e.g. Elasticsearch DSL, Azure AI Search OData filters) belong in a provider-owned type that travels through the common interface without widening it.
