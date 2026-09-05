# RAG ingestion foundations

## Summary
Add a reusable RAG ingestion project for fixed-size chunking and bulk embeddings, with configuration and everyday usage as the primary focus.

Closes #28

## Design artifacts
- [Specification](spec.md)
- [Plan](plan.md)
- [Tasks](tasks.md)
- [Delegated decisions](research.md)

The session user requested a full cycle and delegated questions to subagents; no human GitHub approval is implied by the recorded agent reviews.

## What's New
Implementation pending: Unicode-safe chunking, bounded sequential embedding batches, document pipeline and configuration helpers.

## Testing
Pending implementation and both-framework unit tests. No external integration tests requested.

## Notes
Draft PR opens before production implementation. Base is rag101. No merge/release requested.
