# Documentation Research

## Topics Researched
- OpenAI file_search tool (Responses API)
- OpenAI Vector Stores API
- OpenAI Files API
- Responses API output annotations

## Findings

### file_search Tool (Responses API)

**Source**: https://developers.openai.com/api/docs/guides/tools-file-search
**Relevance**: Core tool this feature wraps — defines the request/response shapes for hosted retrieval.

- Tool entry in `tools` array: `{ "type": "file_search", "vector_store_ids": ["..."], "max_num_results": 50, "filters": {...}, "ranking_options": {...} }`
- `ranking_options` supports `ranker` ("auto", "none", "default-2024-11-15"), `score_threshold`, and `hybrid_search` weights
- Response includes `file_search_call` output items with `status`, `queries`, and optionally `results` (when `include: ["file_search_call.results"]` is set)
- Each result: `{ "file_id", "filename", "score" (0–1), "text", "attributes" }`
- `output_text` blocks carry `annotations` array with `file_citation` entries: `{ "type": "file_citation", "file_id", "filename", "index" }`
- `filters` supports comparison operators (eq, ne, gt, gte, lt, lte, in, nin) on file attributes

---

### Vector Stores API

**Source**: https://developers.openai.com/api/docs/api-reference/vector-stores
**Relevance**: Store/file management surface that `OpenAiVectorStoreClient` will wrap.

- **Create**: POST `/vector_stores` — `name`, `file_ids`, `chunking_strategy` (auto/static), `metadata` (16 KV pairs), `expires_after`
- **List**: GET `/vector_stores` — paginated, cursor-based (`after`, `before`, `limit` 1–100)
- **Retrieve**: GET `/vector_stores/{id}`
- **Modify**: POST `/vector_stores/{id}` — `name`, `metadata`, `expires_after`
- **Delete**: DELETE `/vector_stores/{id}`
- **Search**: POST `/vector_stores/{id}/search` — direct search endpoint (alternative to file_search tool)
- Store status: `expired`, `in_progress`, `completed`
- `file_counts` object: `in_progress`, `completed`, `failed`, `cancelled`, `total`
- **File endpoints**: create (attach file), list, retrieve, delete under `/vector_stores/{id}/files`
- **Batch endpoints**: create (up to 2000 files), retrieve, list files, cancel under `/vector_stores/{id}/file_batches`
- File status lifecycle: `in_progress` → `completed` | `failed` | `cancelled`
- `last_error` on failed files: `code` = `server_error` | `unsupported_file` | `invalid_file`
- `chunking_strategy`: `auto` (800 max tokens, 400 overlap) or `static` (custom `max_chunk_size_tokens` 100–4096, `chunk_overlap_tokens`)

---

### Files API

**Source**: https://developers.openai.com/api/docs/api-reference/files
**Relevance**: File upload surface — prerequisite to attaching files to vector stores.

- **Upload**: POST `/files` — multipart/form-data, `file` + `purpose` (use `"assistants"` for vector store files)
- **List**: GET `/files` — `limit` (1–10000), `purpose` filter, cursor pagination
- **Retrieve**: GET `/files/{file_id}` — metadata only
- **Content**: GET `/files/{file_id}/content` — actual file bytes
- **Delete**: DELETE `/files/{file_id}` — also removes from all vector stores
- Response shape: `{ "id", "object": "file", "bytes", "created_at", "filename", "purpose", "expires_at" }`
- Max 512 MB per file, 2.5 TB per project
- Rate limit: 1,000 uploads/minute

---

### Responses API Annotations

**Source**: https://developers.openai.com/api/docs/api-reference/responses/create
**Relevance**: Existing annotation mapping in the codebase — file_search annotations follow the same pattern.

- `output_text.annotations[]` contains `file_citation` objects for file_search
- `file_citation`: `{ "type": "file_citation", "file_id", "filename", "index" }` — `index` is character position in output text
- Existing codebase already maps `url_citation` annotations to `Citation`/`CitationSource` — `file_citation` follows the same pattern
- `include: ["file_search_call.results"]` in the request to get search results with scores

## Summary

The OpenAI file_search tool integrates into the Responses API `tools` array with `vector_store_ids` binding at the tool level. Results come in two forms: `file_search_call` output items (with search results when `include` is set) and `file_citation` annotations on `output_text` blocks. The Vector Stores API provides full CRUD for stores and files, with an async file processing lifecycle (`in_progress` → `completed`/`failed`/`cancelled`) that requires polling. The Files API handles upload via multipart/form-data with `purpose: "assistants"`.
