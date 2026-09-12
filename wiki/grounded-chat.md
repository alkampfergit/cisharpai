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
| OpenAI | Yes (GPT-5) | Via Responses API `input_file` items; returns `IsSuccess=false` for non-GPT-5 models |
| Azure OpenAI | Yes (GPT-5) | Via Responses API `input_file` items; uses route fallback; returns `IsSuccess=false` if deployment falls back to Chat Completions |
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
- `OpenAiChatCompletionClient` — GPT-5 models only (uses Responses API)
- `AzureOpenAiChatCompletionClient` — GPT-5 deployments only (uses Responses API with route fallback)
- `AnthropicChatCompletionClient` — all Claude models
- `CohereChatCompletionClient` — all Command models

## OpenAI / Azure OpenAI Grounded Chat

OpenAI and Azure OpenAI grounded chat uses the Responses API's `input_file` transport. Documents are base64-encoded and sent as `input_file` items in the `input` array. The model's response includes `file_citation` annotations that are mapped to `Citation`/`CitationSource` records.

### Key differences from Cohere

- **GPT-5 only**: Returns `IsSuccess=false` with a descriptive error for non-GPT-5 models (Legacy, Reasoning). No silent fallback to prompt injection.
- **`CitationMode` ignored**: OpenAI always returns annotations when sources are provided — the `CitationMode` setting has no effect (any value is accepted silently).
- **Citation type**: OpenAI citations have `Type="file_citation"` (vs. Cohere's `"TEXT_CONTENT"`).
- **Citation text**: OpenAI `file_citation` annotations do not include character offsets (`start_index`/`end_index` only appear on `url_citation`/`container_file_citation`). `Citation.Text` is empty and `Citation.Start`/`End` are 0 for OpenAI/Azure citations. Use `CitationSource.Id` to identify which document was cited. `CitationSource.Data` is populated when the source document used structured `Data`.

### Example (OpenAI)

```csharp
// Use OpenAiChatCompletionClient.Create(...) to get an authenticated HttpClient,
// or configure httpClient with OpenAiAuthenticationHandler (see wiki/openai.md).
var client = new OpenAiChatCompletionClient(httpClient, new OpenAiClientOptions
{
    ApiKey = "sk-...",
    DefaultModel = "gpt-5-0513"
});

var groundedFeature = client.Features.Get<IGroundedChatFeature>()!;

var documents = new List<DocumentChunk>
{
    new(Id: "doc-1", Text: "Paris is the capital of France."),
    new(Id: "doc-2", Text: "Berlin is the capital of Germany.")
};

var response = await groundedFeature.GetGroundedChatCompletionAsync(
    new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")]),
    new GroundedChatOptions(Documents: documents));

// response.Citations[0].Sources[0].Id == "doc-1"
```

### Example (Azure OpenAI)

```csharp
// Use AzureOpenAiChatCompletionClient.Create(...) to get an authenticated HttpClient,
// or configure httpClient with AzureAuthenticationHandler (see wiki/azure.md).
var client = new AzureOpenAiChatCompletionClient(httpClient, new AzureOpenAiClientOptions
{
    DeploymentName = "my-gpt5-deployment",
    ModelName = "gpt-5",
    ApiKey = "..."
});

var groundedFeature = client.Features.Get<IGroundedChatFeature>()!;

var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);
```

## Limitations

- **Mutually exclusive with JSON Mode (Cohere)**: The Cohere API does not support `documents` and `response_format` in the same request. Use either grounded chat or JSON output, not both.
- **Model support (Cohere)**: Use Command-R, Command-R+, or Command-A models.
- **GPT-5 only (OpenAI/Azure)**: Non-GPT-5 models return `IsSuccess=false`. This is by design — prompt-injection grounding is a separate feature (#40).

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
| `Citation.Text` is empty (OpenAI/Azure) | OpenAI `file_citation` annotations do not include character offsets. Use `CitationSource.Id` to identify the cited document and `CitationSource.Data` (if structured) for content. |
| "user message" error (OpenAI/Azure) | Grounded chat requires at least one user message to attach documents to. Ensure your request includes a user-role message. |
