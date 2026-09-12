# Spec: Harden bulk embedding for real corpora

**Issue**: #42 | **Branch**: `feature/042-harden-bulk-embedding` | **Date**: 2026-09-12

## Problem

`BulkEmbeddingProcessor` was a good first cut but not safe for indexing a real corpus: fixed batch size with no token awareness, no per-provider ceilings, strictly sequential processing, and no retry on transient failures.

## Changes

### Batch sizing: dual constraint
- **`MaxBatchItems`** (int, default 32) — item ceiling per request. Renamed from `BatchSize`.
- **`MaxBatchTokens`** (int?, default null) — token budget per request. Closes batch when next chunk would exceed budget. First chunk always included.
- **`TokenEstimator`** (`Func<string, int>?`, default `s => s.Length / 4`) — character-based heuristic seam for Phase 2 tokenizer. Validated: must not be null when `MaxBatchTokens` is set.

### Bounded concurrency
- **`MaxConcurrency`** (int, default 1, max 32) — parallel embedding requests via `SemaphoreSlim` + `Channel<T>` re-ordering buffer. `BatchIndex` assigned at collection time; results yielded in input order regardless of completion order.

### Retry
- **`MaxRetries`** (int, default 3) and **`RetryBaseDelay`** (TimeSpan, default 1s) — exponential backoff with jitter for transient failures (429, 5xx). Only the failed batch retries; other batches continue. After exhausting retries, batch surfaces as failed `EmbeddingBatchResult`.
- **`IsTransientError`** (`Func<EmbeddingResponse, bool>?`) — custom transient detection. Default: `BulkEmbeddingProcessor.DefaultIsTransient` (error message heuristics).

### Continue on failure
Failed batches no longer stop the run. Processing continues to the next batch. Network/configuration exceptions still propagate.

### Progress observability
- **`IProgress<BulkEmbeddingProgress>`** parameter added to `EmbedAsync` and `IngestAsync`.
- **`BulkEmbeddingProgress`** record: `CompletedBatches`, `TotalChunksProcessed`, `FailedBatches`.

### Breaking changes (pre-1.0)
- `BatchSize` → `MaxBatchItems`
- `IBulkEmbeddingProcessor.EmbedAsync` and `IRagIngestionPipeline.IngestAsync` gain `IProgress<BulkEmbeddingProgress>?` parameter (default null).
- Failed batches no longer stop enumeration.

## Design decisions
1. **Transient detection via callback on options** rather than `IsTransient` on `EmbeddingResponse` — avoids changing the core project.
2. **Default `MaxBatchItems` = 32** — conservative global default; per-provider ceilings are documentation-only.
3. **`IProgress<T>` on interface** — standard .NET pattern; pre-1.0 breaking change is acceptable.
