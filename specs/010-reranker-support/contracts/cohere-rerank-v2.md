# Contract: Cohere Rerank v2

`POST {BaseUrl}rerank` — with the default `BaseUrl` this resolves to
`https://api.cohere.com/v2/rerank`.

## Headers

| Header | Value |
|--------|-------|
| `Authorization` | `Bearer {ApiKey}` (applied by `CohereAuthenticationHandler`) |
| `Content-Type` | `application/json` |

## Request body

```json
{
  "model": "rerank-v3.5",
  "query": "What is the capital of France?",
  "documents": [
    "Carson City is the capital city of Nevada.",
    "Paris is the capital and most populous city of France.",
    "The Louvre is a museum in Paris."
  ],
  "top_n": 2,
  "max_tokens_per_doc": 4096
}
```

`top_n` and `max_tokens_per_doc` are omitted when null. Anything supplied through
`ExtraParameters` is deep-merged over this body — for example:

```json
{ "priority": 500 }
```

## Success response (200)

```json
{
  "id": "07734bd2-2473-4f07-94e1-0d9f0e6843cf",
  "results": [
    { "index": 1, "relevance_score": 0.999071 },
    { "index": 2, "relevance_score": 0.752048 }
  ],
  "meta": {
    "api_version": { "version": "2" },
    "billed_units": { "search_units": 1 }
  }
}
```

- `results` is ordered by descending `relevance_score`.
- `index` refers to the position in the submitted `documents` array.
- `id` is a request identifier, **not** a model name.

## Error response (4xx / 5xx)

```json
{ "message": "invalid api token" }
```

Surfaced as `RerankResponse.Error(...)`: `IsSuccess = false`, `ErrorMessage` set from
`LlmHttpRequestException.Message`, `RawResponseJson` carrying the body. No exception is thrown.

## Alternative hosting

Set `CohereClientOptions.BaseUrl` to an Azure AI Foundry (or other) deployment; the `rerank`
path is appended relatively and the contract above is unchanged.
