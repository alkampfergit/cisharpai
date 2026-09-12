# Data Model: Reranker Support

## Unified models (`Cisharpai.Models`)

### RerankRequest

```csharp
public sealed record RerankRequest(
    string Query,
    IReadOnlyList<string> Documents,
    string? Model = null,
    int? TopN = null,
    int? MaxTokensPerDocument = null,
    bool IncludeRawResponse = false,
    JsonElement? ExtraParameters = null);
```

| Field | Meaning |
|-------|---------|
| `Query` | The search query the documents are ranked against. |
| `Documents` | Candidate documents, in the caller's own order. `RerankResult.Index` refers back into this list. |
| `Model` | Provider model id. Falls back to `DefaultModel` in options; if both are absent the call throws. |
| `TopN` | Return only the top N results. `null` = all. |
| `MaxTokensPerDocument` | Per-document truncation budget. |
| `IncludeRawResponse` | Populates `RawRequestJson` / `RawResponseJson`. |
| `ExtraParameters` | Deep-merged into the request body (e.g. Cohere's `priority`). |

### RerankResult

```csharp
public sealed record RerankResult(
    int Index,
    double RelevanceScore);
```

`Index` is the zero-based position in the request's `Documents`. `RelevanceScore` is the
provider's relevance score; scales are provider-defined, so compare within one response rather
than across providers.

### RerankResponse

```csharp
public sealed record RerankResponse(
    IReadOnlyList<RerankResult> Results,
    string Model,
    int? SearchUnits = null,
    int? InputTokens = null,
    string? RawResponseJson = null,
    string? RawRequestJson = null,
    bool IsSuccess = true,
    string? ErrorMessage = null)
{
    public static RerankResponse Error(string errorMessage, string? rawResponseJson = null);
}
```

`Results` preserves the provider's ordering (most relevant first). `SearchUnits` and
`InputTokens` are both nullable because providers bill differently — Cohere reports search
units, others may report tokens.

## Provider models (`Cisharpai.Cohere.Models`)

Serialized with `JsonNamingPolicy.SnakeCaseLower` and `JsonIgnoreCondition.WhenWritingNull`.

### CohereRerankRequest

| Property | Wire name | Source |
|----------|-----------|--------|
| `Model` | `model` | resolved model |
| `Query` | `query` | `RerankRequest.Query` |
| `Documents` | `documents` | `RerankRequest.Documents` |
| `TopN` | `top_n` | `RerankRequest.TopN` |
| `MaxTokensPerDoc` | `max_tokens_per_doc` | `RerankRequest.MaxTokensPerDocument` |

### CohereRerankResponse

| Property | Wire name |
|----------|-----------|
| `Id` | `id` |
| `Results` | `results` (`{ index, relevance_score }`) |
| `Meta.BilledUnits.SearchUnits` | `meta.billed_units.search_units` |
| `Meta.BilledUnits.InputTokens` | `meta.billed_units.input_tokens` |

## Mapping

| Unified | Cohere |
|---------|--------|
| `RerankResponse.Results[i].Index` | `results[i].index` |
| `RerankResponse.Results[i].RelevanceScore` | `results[i].relevance_score` |
| `RerankResponse.Model` | the resolved request model (the response `id` is a request id, not a model) |
| `RerankResponse.SearchUnits` | `meta.billed_units.search_units` |
| `RerankResponse.InputTokens` | `meta.billed_units.input_tokens` |

## Error states

| Condition | Result |
|-----------|--------|
| Non-2xx HTTP | `RerankResponse.Error(message, responseBody)` — `IsSuccess = false` |
| Malformed / empty body | `RerankResponse.Error(message)` |
| No model on request and no `DefaultModel` | throws `InvalidOperationException` |
| Network failure | propagates (per constitution principle II) |
