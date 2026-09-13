# Grounded Chat (RAG)

## Overview

Grounded chat enables document-grounded Q&A with source citations. Available via `IGroundedChatFeature` on all five providers: **OpenAI** (GPT-5, native), **Azure OpenAI** (GPT-5 deployments, native), **Azure AI Inference** (all models, synthesized fallback), **Anthropic** (all Claude models, native), and **Cohere** (all Command models, native).

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

| Mode | Cohere | Anthropic | Azure AI Inference (fallback) |
|------|--------|-----------|-------------------------------|
| `CitationMode.Accurate` | Full response first, then citations. Only `command-r` family. | Treated as `Enabled` (warning logged). | No distinction — same as Fast. |
| `CitationMode.Fast` (default) | Inline citations during generation. All models. | Treated as `Enabled` (warning logged). | Same behavior for all modes. |
| `CitationMode.Enabled` | Provider default. | Citations enabled. | Same behavior for all modes. |

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
- `GroundingKind` — `GroundingKind.Native` (default) or `GroundingKind.Synthesized`

### GroundingKind

`GroundingKind.Native` — provider has first-class citation support (Cohere, Anthropic, OpenAI/Azure OpenAI).
`GroundingKind.Synthesized` — citations produced via prompt injection and marker parsing (Azure AI Inference).

```csharp
if (response.GroundingKind == GroundingKind.Synthesized)
    Console.WriteLine("Citations are AI-generated, not provider-verified.");
```

## Best Practices

- Keep document chunks ~300-400 words for optimal performance
- Use structured format with meaningful field names
- Unique IDs for each document chunk
- Use `CitationMode.Enabled` for cross-provider compatibility

## OpenAI / Azure OpenAI Grounded Chat

GPT-5 models support grounded chat via the Responses API `input_file` transport. Documents are base64-encoded and sent as `input_file` items. Response annotations (`file_citation`) are mapped to `Citation`/`CitationSource`.

- **GPT-5 only**: Non-GPT-5 models return `IsSuccess=false` (no silent fallback).
- **`CitationMode` ignored**: OpenAI always returns annotations when sources are provided.
- **Azure route fallback**: If the deployment falls back from Responses API to Chat Completions, grounded chat returns `IsSuccess=false`.

```csharp
// OpenAI GPT-5
var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions { DefaultModel = "gpt-5-0513" });
var feature = client.Features.Get<IGroundedChatFeature>()!;
var response = await feature.GetGroundedChatCompletionAsync(request, options);
```

## Azure AI Inference Grounded Chat (Fallback)

Azure AI Inference models (Phi-3, Llama-3, Mistral, etc.) use the prompt-injection fallback via `GroundedChatFallbackHelper`:

1. Documents serialized into a context block in the system message
2. Model instructed to emit `«cite:N»…«/cite»` guillemet markers
3. Markers regex-matched and stripped to produce clean content with citation offsets

```csharp
var client = new AzureAiInferenceChatCompletionClient(httpClient, options);
var feature = client.Features.Get<IGroundedChatFeature>()!;
var response = await feature.GetGroundedChatCompletionAsync(request, groundedOptions);
// response.GroundingKind == GroundingKind.Synthesized
```

- Graceful degradation: no markers → zero citations, `IsSuccess=true`
- Citation type: `"synthesized_citation"`
- All `CitationMode` values accepted (no server-side distinction)

## Limitations

- **Cohere**: mutually exclusive with JSON Mode — cannot combine grounded chat and JSON output
- **Cohere**: supported models — Command-R, Command-R+, Command-A only
- **OpenAI/Azure: GPT-5 only** — Non-GPT-5 models return `IsSuccess=false` with a descriptive error
- **Azure AI Inference (fallback)** — citation quality depends on model's instruction-following ability; smaller models may not always emit markers
- **Anthropic**: `CitationMode.Fast`/`Accurate` are treated as `Enabled`; PDF/base64 sources not yet first-class (reachable via `ExtraParameters`)
