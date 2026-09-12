# Public API contract

Namespace root `Cisharpai.Rag`; models in `.Models`, chunker in `.Chunking`, bulk in `.Embeddings`.

- `ITextChunker.Chunk(RagDocument)` returns lazy `IEnumerable<TextChunk>`.
- `FixedSizeChunker(FixedSizeChunkerOptions? options = null)` snapshots validated options.
- `IBulkEmbeddingProcessor.EmbedAsync(IAsyncEnumerable<TextChunk>, CancellationToken)` returns `IAsyncEnumerable<EmbeddingBatchResult>`; an IEnumerable overload supports ordinary collections.
- `BulkEmbeddingProcessor(IEmbeddingClient, BulkEmbeddingOptions? options = null)` snapshots options and retains scoped provider.
- `IRagIngestionPipeline.IngestAsync(IAsyncEnumerable<RagDocument>, CancellationToken)` and IEnumerable overload compose chunking with bulk processing.
- `RagIngestionPipeline(ITextChunker, IBulkEmbeddingProcessor)` supports direct composition.
- `services.AddCisharpaiRag(Action<RagOptions>? configure = null)` resolves unkeyed IEmbeddingClient.
- `services.AddCisharpaiRag(Func<IServiceProvider,IEmbeddingClient> embeddingClientFactory, Action<RagOptions>? configure = null)` supports keyed clients.

DI uses scoped processors/pipelines to avoid capturing scoped providers in singletons; chunker is stateless after options snapshot. Configuration callbacks may bind from the host configuration package. Invalid options fail at resolution/construction before API traffic. Registration preserves custom interfaces using TryAdd where appropriate.

Batch outputs preserve input order and source associations; failures return no Items. Validate successful vector count, nonempty consistent lengths, requested dimensions and finite values. Retain raw payloads and provider metadata on validation failure. Do not change a provider's existing error message. Source enumeration is lazy and disposed on completion, failure, cancellation or consumer break.

Caller-provided chunks require a nonblank document ID, nonnull text and nonnegative index/offset. Invalid input throws ArgumentException (or a subclass) before submitting that batch. Empty text is permitted but may be rejected by the chosen provider.
