# Data model

- `RagDocument(string Id, string Text)`: identified source text; nonblank ID, nonnull text. Empty text allowed.
- `TextChunk(string DocumentId, int Index, int StartOffset, string Text)`: zero-based chunk index and UTF-16 source start; exact slice.
- `ChunkEmbedding(TextChunk Chunk, float[] Vector)`: paired valid float vector.
- `EmbeddingBatchResult(long BatchIndex, IReadOnlyList<TextChunk> Chunks, IReadOnlyList<ChunkEmbedding> Items, EmbeddingResponse Response)`: success/error convenience properties forward Response; retain original metadata/raw JSON.
- `FixedSizeChunkerOptions`: ChunkSize 1024, Overlap 128; positive size and overlap in [0,size).
- `BulkEmbeddingOptions`: BatchSize 32, optional Model, InputType Document, optional Dimensions, IncludeRawResponse false, optional ExtraParameters. Positive batch/dimensions. Output encoding float.
- `RagOptions`: Chunking and Embedding option objects used by registration.

Batch lifecycle: collect up to BatchSize → submit one request → validate → yield success or failure. Failure terminates enumeration; successful batches continue. Cancellation throws, never yields an ordinary error.
