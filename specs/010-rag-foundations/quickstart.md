# Quickstart acceptance scenarios

The complete copyable guide will be in `wiki/rag.md` and the new project README.

1. Construct FixedSizeChunker with size 1024 / overlap 128 and chunk a RagDocument.
2. Construct BulkEmbeddingProcessor around an existing embedding client and enumerate batch results from chunk input. Handle IsSuccess before consuming Items.
3. Register an existing provider, then AddCisharpaiRag with Chunking and Embedding settings. Resolve IRagIngestionPipeline in a scope and ingest document input.
4. Bind a Rag configuration section in the registration callback when host configuration binding is available.
5. Supply a factory using GetRequiredKeyedService<IEmbeddingClient> to select a provider.
6. Pass CancellationToken and process each batch immediately; avoid ToList over an entire corpus.

Tests reproduce these flows with FakeEmbeddingClient/NSubstitute without credentials or network APIs. Provider model/batch/token limits are caller configuration concerns.
