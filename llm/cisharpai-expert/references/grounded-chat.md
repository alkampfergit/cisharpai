# Grounded Chat (RAG) — Anthropic & Cohere

## Overview

Grounded chat enables document-grounded Q&A with source citations. Available via `IGroundedChatFeature` on the Anthropic and Cohere providers.

## Quick Start

```csharp
var groundedFeature = client.Features.Get<IGroundedChatFeature>();

var documents = new[]
{
    new DocumentChunk("doc-1", new Dictionary<string, string>
    {
        ["title"] = "Company Policy",
        ["snippet"] = "All employees must complete security training annually."
    }),
    new DocumentChunk("doc-2", new Dictionary<string, string>
    {
        ["title"] = "HR FAQ",
        ["snippet"] = "New employees have 30 days to complete onboarding."
    })
};

var response = await groundedFeature.GetGroundedChatCompletionAsync(
    request,
    new GroundedChatOptions(
        Documents: documents,
        CitationMode: CitationMode.Enabled));

Console.WriteLine(response.Content);
foreach (var citation in response.Citations)
{
    Console.WriteLine($"  [{citation.Start}-{citation.End}] \"{citation.Text}\"");
    foreach (var src in citation.Sources)
    {
        Console.WriteLine($"    Source: {src.Id}");
        if (src.CitedText is not null)
            Console.WriteLine($"    Cited: {src.CitedText}");
    }
}
```

## Document Formats

### Structured (Key-Value)

```csharp
new DocumentChunk("doc-1", new Dictionary<string, string>
{
    ["title"] = "Document Title",
    ["snippet"] = "Document content here...",
    ["author"] = "Jane Doe"
})
```

**Cohere**: serialised as JSON into the `data` field.
**Anthropic**: mapped to `custom_content` source type with one text content block per key-value pair.

### Plain Text

```csharp
new DocumentChunk("doc-1", "Plain text content of the document...")
```

**Cohere**: sent as a string in the `data` field.
**Anthropic**: sent as a `text` source type with `media_type: "text/plain"`.

`Data` and `Text` are mutually exclusive — use one or the other.

## Citation Modes

| Mode | Cohere | Anthropic |
|------|--------|-----------|
| `CitationMode.Accurate` | Full response first, then citations. Only `command-r` family. | Treated as `Enabled` (warning logged). |
| `CitationMode.Fast` (default) | Inline citations during generation. All models. | Treated as `Enabled` (warning logged). |
| `CitationMode.Enabled` | Provider default. | Citations enabled. |

> **Cohere model compatibility.** `Accurate` is supported only by the `command-r`
> family. `command-a` models reject it; the provider logs a warning and downgrades
> to `Fast`.

> **Anthropic.** Citations are binary (enabled or not). `Fast` and `Accurate` are
> both treated as `Enabled` with a logged warning.

## Citation Model

**Citation:**
- `Start` — Character offset in the response content (inclusive)
- `End` — Character offset in the response content (exclusive)
- `Text` — The response text span that is cited
- `Sources` — Array of `CitationSource`
- `Type` — Provider-specific type (e.g. `"TEXT_CONTENT"` for Cohere, `"char_location"` for Anthropic)

**CitationSource:**
- `Id` — Document chunk ID
- `Data` — Optional key-value metadata (populated when `DocumentChunk.Data` was used)
- `CitedText` — Text from the source document that was cited (Anthropic only; `null` for Cohere)

### `Start`/`End` Semantics

Both providers report offsets within the concatenated response content.

**Cohere**: one citation per inline text span. Offsets point precisely to the cited words.

**Anthropic**: offsets correspond to the text block carrying the citation. Multiple citations may share the same `Start`/`End` when a text block references several sources. Use `CitationSource.CitedText` to distinguish.

## GroundedChatCompletionResponse

Wraps `ChatCompletionResponse` and adds:
- `Citations` — List of `Citation` objects

## Best Practices

- Keep document chunks ~300-400 words for optimal performance
- Use structured format with meaningful field names
- Unique IDs for each document chunk
- Use `CitationMode.Enabled` for cross-provider compatibility

## Limitations

- **OpenAI, Azure OpenAI, Azure AI Inference** — return `null` for `Features.Get<IGroundedChatFeature>()`
- **Cohere**: mutually exclusive with JSON Mode — cannot combine grounded chat and JSON output
- **Anthropic**: `CitationMode.Fast`/`Accurate` are treated as `Enabled`; PDF/base64 sources not yet first-class (reachable via `ExtraParameters`)
