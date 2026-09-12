# Grounded Chat (RAG with Document Citations)

## Overview

Cisharpai supports grounded chat (Retrieval-Augmented Generation) via the `IGroundedChatFeature`. This feature lets you pass document chunks alongside your question, and the model answers **only** from the provided documents, returning citations with character offsets pointing back to the source material.

This is ideal for:
- **Q&A over internal documents** where hallucination must be minimized
- **Customer support bots** that answer strictly from knowledge base articles
- **Research assistants** that cite their sources

## Quick Start

### Structured Documents (Key-Value)

```csharp
var client = provider.GetRequiredService<IChatCompletionClient>();
var groundedFeature = client.Features.Get<IGroundedChatFeature>();

if (groundedFeature is not null)
{
    var documents = new List<DocumentChunk>
    {
        new(Id: "doc-1", Data: new Dictionary<string, string>
        {
            ["title"] = "France",
            ["snippet"] = "Paris is the capital city of France."
        }),
        new(Id: "doc-2", Data: new Dictionary<string, string>
        {
            ["title"] = "Germany",
            ["snippet"] = "Berlin is the capital city of Germany."
        })
    };

    var request = new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
        Model: "command-a-03-2025",
        Temperature: 0);

    var options = new GroundedChatOptions(Documents: documents);

    var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);
    // response.Content contains the answer
    // response.Citations contains source references
}
```

### Plain Text Documents

```csharp
var documents = new List<DocumentChunk>
{
    new(Id: "doc-1", Text: "Paris is the capital city of France."),
    new(Id: "doc-2", Text: "Berlin is the capital city of Germany.")
};

var options = new GroundedChatOptions(Documents: documents);
var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);
```

## Provider Support Matrix

| Provider | Grounded Chat | Notes |
|----------|--------------|-------|
| OpenAI | -- | Not supported |
| Azure OpenAI | -- | Not supported |
| Azure AI Inference | -- | Not supported |
| Anthropic | Yes | Via `document` content blocks with `citations: {enabled: true}` in the Messages API |
| Cohere | Yes | Via `documents` array and `citation_options` in Chat v2 API |

## Citation Modes

| Mode | Description |
|------|-------------|
| `CitationMode.Accurate` | Model generates the full response first, then produces fine-grained citations. Higher latency, more precise. **Cohere**: only supported by the `command-r` family — `command-a` models reject this value (the provider logs a warning and downgrades to `Fast`). **Anthropic**: treated as `Enabled` (warning logged; citations are binary on/off). |
| `CitationMode.Fast` (default) | Citations generated inline as the response is produced. Lower latency, slightly less precise. **Cohere**: supported by both `command-r` and `command-a` families. **Anthropic**: treated as `Enabled` (warning logged). |
| `CitationMode.Enabled` | Provider-default citation behavior. Both Cohere and Anthropic honour this directly. |

```csharp
var options = new GroundedChatOptions(
    Documents: documents,
    CitationMode: CitationMode.Fast);
```

## Document Formats

### Structured (Key-Value)

Use `Data` for documents with metadata fields like title, snippet, author, etc. The model sees all fields.

```csharp
new DocumentChunk(
    Id: "doc-1",
    Data: new Dictionary<string, string>
    {
        ["title"] = "My Document",
        ["snippet"] = "The actual content...",
        ["author"] = "Jane Doe"
    })
```

### Plain Text

Use `Text` for simple text content.

```csharp
new DocumentChunk(Id: "doc-1", Text: "The actual content of the document.")
```

**Note:** `Data` and `Text` are mutually exclusive — set exactly one.

**Tip:** Keep document chunks to approximately 300-400 words or less for optimal model performance.

## Working with Citations

Each `Citation` in the response has:
- `Start` / `End`: Character offsets (inclusive/exclusive) in the response content
- `Text`: The cited text span
- `Sources`: List of `CitationSource` objects, each with an `Id`, optional `Data` dictionary, and optional `CitedText`

### `Start`/`End` Semantics Per Provider

| Provider | `Start`/`End` Meaning |
|----------|----------------------|
| Cohere | Character offsets within the concatenated response content, pointing to the response text span that is backed by the citation. One citation per inline span. |
| Anthropic | Character offsets within the concatenated response content, corresponding to the text block that carries the citation. Multiple citations may share the same `Start`/`End` range when a text block references several sources. |

### `CitedText` on `CitationSource`

Anthropic returns the exact text from the *source document* that was cited (`cited_text`). This is available via `CitationSource.CitedText`. Cohere does not provide this — `CitedText` will be `null` for Cohere citations.

```csharp
var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);

foreach (var citation in response.Citations)
{
    // Verify the offset matches
    var textAtOffset = response.Content[citation.Start..citation.End];
    Debug.Assert(textAtOffset == citation.Text);

    foreach (var source in citation.Sources)
    {
        Console.WriteLine($"Cited from document: {source.Id}");
    }
}
```

## Response Structure

`GroundedChatCompletionResponse` wraps the standard `ChatCompletionResponse` with citations:

```csharp
var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);

// Convenience properties (delegate to ChatCompletion)
response.IsSuccess       // bool
response.Content         // string
response.ErrorMessage    // string?

// Full response details
response.ChatCompletion.PromptTokens
response.ChatCompletion.CompletionTokens
response.ChatCompletion.RawResponseJson  // when IncludeRawResponse = true

// Citations
response.Citations       // IReadOnlyList<Citation>
```

## Feature Discovery

```csharp
IChatCompletionClient client = /* any provider */;

if (client.Features.Get<IGroundedChatFeature>() is { } groundedFeature)
{
    // Grounded chat is supported
}
else
{
    // Fallback: use regular chat completion
}
```

Currently, `IGroundedChatFeature` is registered on:
- `AnthropicChatCompletionClient`
- `CohereChatCompletionClient`

## Limitations

### Cohere
- **Mutually exclusive with JSON Mode**: The Cohere API does not support `documents` and `response_format` in the same request. Use either grounded chat or JSON output, not both.
- **Model support**: Not all Cohere models support grounded chat. Use Command-R, Command-R+, or Command-A models.

### Anthropic
- **Citation modes are binary**: Anthropic citations are enabled or disabled — there is no accuracy/speed tradeoff. `CitationMode.Fast` and `CitationMode.Accurate` are treated as `Enabled` with a logged warning.
- **PDF / base64 sources**: Only `text` and `custom_content` source types are mapped. Anthropic's richer source types (PDF base64 etc.) are reachable via `ExtraParameters` and may get first-class mapping in a future release.
- **Offset granularity**: Each text block maps to one citation span. If a text block carries multiple citations from different documents, they share the same `Start`/`End` range; use `CitationSource.CitedText` to distinguish.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No citations returned | The model may not find relevant information in the provided documents. Ensure documents contain relevant content. |
| Empty response | Check `response.IsSuccess` and `response.ErrorMessage` for API errors. |
| Citation offsets incorrect (Cohere) | For the most precise offsets request `CitationMode.Accurate` against a `command-r` model. `command-a` models do not support `Accurate`; the provider downgrades to `Fast` and logs a warning. |
| "documents not supported" error (Cohere) | Check that you're using a supported model (Command-R, Command-R+, Command-A). |
| `CitedText` is null (Cohere) | This field is only populated by Anthropic. Cohere does not return source-level cited text. |
