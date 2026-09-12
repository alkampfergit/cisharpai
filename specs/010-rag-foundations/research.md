# Research and delegated design decisions

Source: rag_design subagent inspected existing provider models, registration methods, fake client and repository rules on 2026-09-05.

| Question | Decision | Rationale / alternatives |
|---|---|---|
| Chunk units | Unicode scalar values; UTF-16 offsets | Preserves supplementary characters; raw UTF-16 chunks can split pairs, tokenization adds provider-specific dependencies. |
| Bulk shape | Lazy async batch results, sequential batch size 32 | Bounded resources and ordering; concurrent scheduling and provider async job APIs deferred. |
| Error policy | Yield failed batch then stop | Prior successes visible; retries remain provider HTTP policy; no silent partial Zip mapping. |
| Response integrity | Validate count, nonempty finite vectors and dimensions | Prevent malformed successes corrupting downstream indexes; preserve provider metadata via record copy on error. |
| Cancellation | Check before and after provider await | Some existing providers catch cancellation into error responses. |
| Configuration | Snapshot validated nested options; provider factory overload | Direct usage and host binding without extra configuration dependency; factory supports keyed/scoped clients. |
| Tests | Existing single NUnit project | Required by constitution; a separate RAG test folder provides clear ownership. |
| Grounding model | New TextChunk record | Existing DocumentChunk describes grounded chat payloads and must remain compatible. |

Options retained beyond construction must be snapshotted, including cloning ExtraParameters. Empty documents yield no chunks; no whitespace normalization; document identity uniqueness belongs to caller. No corpus-wide duplicate tracking.
