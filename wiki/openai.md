# OpenAI Provider

**Package:** `Cisharpai.OpenAi`

## Setup

```csharp
services.AddOpenAiClient(options =>
{
    options.ApiKey = "sk-...";
    options.BaseUrl = "https://api.openai.com/v1";  // default; override for proxies
    options.Organization = "org-...";                // optional
    options.DefaultModel = "gpt-4.1";               // optional fallback
    options.ReasoningEffort = ReasoningEffort.High;  // for o-series models
    options.TextVerbosity = TextVerbosity.Concise;   // for Responses API (GPT-5)
});
```

## Available Models

Use the typed constants from `OpenAiModels` to avoid typos:

| Constant | Model ID |
|----------|----------|
| `OpenAiModels.Chat.Gpt4_1` | `gpt-4.1` |
| `OpenAiModels.Chat.Gpt4_1Mini` | `gpt-4.1-mini` |
| `OpenAiModels.Chat.Gpt4_1Nano` | `gpt-4.1-nano` |
| `OpenAiModels.Chat.O3` | `o3` |
| `OpenAiModels.Chat.O3Mini` | `o3-mini` |
| `OpenAiModels.Chat.O4Mini` | `o4-mini` |
| `OpenAiModels.Chat.Gpt5` | `gpt-5` |
| `OpenAiModels.Embedding.TextEmbedding3Small` | `text-embedding-3-small` |
| `OpenAiModels.Embedding.TextEmbedding3Large` | `text-embedding-3-large` |

## Supported Features

| Feature | Interface |
|---------|-----------|
| Chat completions | `IChatCompletionClient` |
| Text embeddings | `IEmbeddingClient` |
| JSON Mode & Structured Outputs | `IJsonOutputFeature` |
| Grounded Chat (RAG) | `IGroundedChatFeature` |
| Tool / function calling | `IToolCallingFeature` |
| Streaming | `IStreamingChatFeature` |
| Vision (image input) | `LlmMessage.WithImage()` |

## Automatic API Routing

The client selects the correct OpenAI API surface automatically based on the model name:

| Model Pattern | API Used | Notes |
|---------------|----------|-------|
| `gpt-5*` | Responses API | Uses `text.format` for JSON, `response.completed` for stream end |
| `o1*`, `o3*`, `o4*` | Chat Completions — reasoning mode | Uses `max_completion_tokens`, sends `reasoning_effort` |
| Everything else | Chat Completions — standard | Legacy and current GPT-4 models |

You do not need to configure routing manually.

## Reasoning Models (o-series)

Set `ReasoningEffort` globally in options — it is sent only for reasoning model requests:

```csharp
services.AddOpenAiClient(o =>
{
    o.ApiKey = "sk-...";
    o.ReasoningEffort = ReasoningEffort.High; // Low, Medium, High
});

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Solve this step by step: ...")],
    Model: OpenAiModels.Chat.O4Mini);
```

## Responses API (GPT-5)

GPT-5 routes automatically to the Responses API. Configure response verbosity:

```csharp
services.AddOpenAiClient(o =>
{
    o.ApiKey = "sk-...";
    o.TextVerbosity = TextVerbosity.Concise; // Concise, Default, Verbose
});
```

Streaming for GPT-5 uses `response.completed` as the termination event instead of `[DONE]`. This is handled internally — your streaming code is identical across models.

## Grounded Chat (RAG with Citations)

GPT-5 models support grounded chat via the Responses API's `input_file` transport. Documents are base64-encoded and included as `input_file` items in the `input` array. The model returns `file_citation` annotations that map to `Citation`/`CitationSource` records.

```csharp
var groundedFeature = client.Features.Get<IGroundedChatFeature>()!;

var documents = new List<DocumentChunk>
{
    new(Id: "doc-1", Text: "Paris is the capital of France."),
    new(Id: "doc-2", Text: "Berlin is the capital of Germany.")
};

var response = await groundedFeature.GetGroundedChatCompletionAsync(
    new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
        Model: "gpt-5-0513"),
    new GroundedChatOptions(Documents: documents));
```

Non-GPT-5 models (GPT-4, o-series) return `IsSuccess=false` with a descriptive error — there is no silent fallback. `CitationMode` is accepted but ignored (OpenAI always returns annotations when sources are provided). See [Grounded Chat](grounded-chat.md) for full details.

## Text Embeddings

```csharp
var embedClient = provider.GetRequiredService<IEmbeddingClient>();

var response = await embedClient.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world", "Another sentence"],
    Model: OpenAiModels.Embedding.TextEmbedding3Small,
    Dimensions: 256));  // optional dimension reduction

if (response.IsSuccess)
    foreach (var vector in response.Embeddings)
        Console.WriteLine($"Dimensions: {vector.Length}");
```

## Debugging

Set `IncludeRawResponse: true` to inspect the exact request and response JSON:

```csharp
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello")],
    Model: OpenAiModels.Chat.Gpt4_1Nano,
    IncludeRawResponse: true);

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.RawRequestJson);
Console.WriteLine(response.RawResponseJson);
```

## See Also

- [Getting Started](getting-started.md) — general setup and DI registration
- [Provider Feature Matrix](provider-features.md) — full capability comparison
- [Streaming](streaming.md) — `IStreamingChatFeature`
- [Tool Calling](tool-calling.md) — `IToolCallingFeature`
- [JSON Output](json-output.md) — `IJsonOutputFeature`
- [Vision](vision.md) — image inputs
