# Grounded Chat (RAG) — Cohere Only

## Overview

Grounded chat enables document-grounded Q&A with source citations. Available exclusively via `IGroundedChatFeature` on the Cohere provider.

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
        CitationMode: CitationMode.Accurate));

Console.WriteLine(response.Content);
foreach (var citation in response.Citations)
{
    Console.WriteLine($"  [{citation.Start}-{citation.End}] \"{citation.Text}\"");
    foreach (var src in citation.Sources)
        Console.WriteLine($"    Source: {src.Id}");
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

### Plain Text

```csharp
new DocumentChunk("doc-1", "Plain text content of the document...")
```

`Data` and `Text` are mutually exclusive — use one or the other.

## Citation Modes

| Mode | Behavior | Latency |
|------|----------|---------|
| `CitationMode.Accurate` | Full response first, then citations | Higher, more precise |
| `CitationMode.Fast` | Inline citations during generation | Lower, less precise |
| `CitationMode.Enabled` | Provider default | Varies |

## Citation Model

**Citation:**
- `Start` — Character offset (start)
- `End` — Character offset (end)
- `Text` — Cited text span
- `Sources` — Array of `CitationSource`

**CitationSource:**
- `Id` — Document chunk ID
- `Data` — Optional key-value metadata

## GroundedChatCompletionResponse

Wraps `ChatCompletionResponse` and adds:
- `Citations` — List of `Citation` objects

## Best Practices

- Keep document chunks ~300-400 words for optimal performance
- Use structured format with meaningful field names
- Unique IDs for each document chunk

## Limitations

- **Cohere only** — Other providers return `null` for `Features.Get<IGroundedChatFeature>()`
- **Mutually exclusive with JSON Mode** — Cannot combine grounded chat and JSON output
- **Supported models:** Command-R, Command-R+, Command-A only
