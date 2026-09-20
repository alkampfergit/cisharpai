# Data model

- `RagDocument(string Id, string Text)`: identified source text; nonblank ID, nonnull text. Empty text allowed.
- `TextChunk(string DocumentId, int Index, int StartOffset, int EndOffset, string Text, IReadOnlyDictionary<string, object?> Metadata)`: zero-based chunk index, UTF-16 source start and exclusive end offset, chunk text, and chunker-specific metadata (defensively copied, empty by default, never null). `StartOffset`/`EndOffset` always delimit the original source span; `Text` is the verbatim slice for built-in chunkers but may differ for non-verbatim chunkers.
- `ChunkEmbedding(TextChunk Chunk, float[] Vector)`: paired valid float vector.
- `EmbeddingBatchResult(long BatchIndex, IReadOnlyList<TextChunk> Chunks, IReadOnlyList<ChunkEmbedding> Items, EmbeddingResponse Response)`: success/error convenience properties forward Response; retain original metadata/raw JSON.
- `FixedSizeChunkerOptions`: ChunkSize 1024, Overlap 128; positive size and overlap in [0,size).
- `BulkEmbeddingOptions`: MaxBatchItems 32, optional MaxBatchTokens, optional TokenEstimator (`s => s.Length / 4` default), MaxConcurrency 1, optional MaxPendingBatches (defaults to `MaxConcurrency * 2`), MaxRetries 3, RetryBaseDelay 1s, optional IsTransientError, optional Model, InputType Document, optional Dimensions, IncludeRawResponse false, optional ExtraParameters. Positive batch/dimensions. Output encoding float.
- `RagOptions`: Chunking and Embedding option objects used by registration.

Batch lifecycle: collect up to MaxBatchItems (or until MaxBatchTokens budget is exceeded) → submit one request → validate → yield success or failure. Failure yields a failed `EmbeddingBatchResult` and processing continues to the next batch. Cancellation throws, never yields an ordinary error. With `MaxConcurrency > 1`, multiple batches may be in flight; results are reordered to preserve input order.
