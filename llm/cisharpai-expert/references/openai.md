# OpenAI Provider

## Setup

```csharp
services.AddOpenAiClient(options =>
{
    options.ApiKey = "sk-...";
    options.BaseUrl = "https://api.openai.com/v1";  // default
    options.Organization = "org-...";                // optional
    options.DefaultModel = "gpt-4o";                 // optional
});
```

**Options:**
- `ApiKey` (required)
- `BaseUrl` — Override for proxies or compatible APIs
- `Organization` — OpenAI org ID
- `DefaultModel` — Fallback when request omits `Model`
- `ReasoningEffort` — For o-series models: `Low`, `Medium`, `High`
- `TextVerbosity` — For Responses API: `Concise`, `Default`, `Verbose`

## Model Routing

The client automatically routes requests based on the model name:

| Model Pattern | API Route | Notes |
|---------------|-----------|-------|
| `gpt-5*` | Responses API | Latest API surface |
| `o1*`, `o3*`, `o4*` | Reasoning mode | Uses `reasoning_effort` |
| Everything else | Chat Completions API | Legacy/standard |

## Available Models (`OpenAiModels`)

**Chat:**
- `OpenAiModels.Chat.Gpt4_1` — `gpt-4.1`
- `OpenAiModels.Chat.Gpt4_1Mini` — `gpt-4.1-mini`
- `OpenAiModels.Chat.Gpt4_1Nano` — `gpt-4.1-nano`
- `OpenAiModels.Chat.O3` — `o3`
- `OpenAiModels.Chat.O3Mini` — `o3-mini`
- `OpenAiModels.Chat.O4Mini` — `o4-mini`
- `OpenAiModels.Chat.Gpt5` — `gpt-5`

**Embedding:**
- `OpenAiModels.Embedding.TextEmbedding3Small` — `text-embedding-3-small`
- `OpenAiModels.Embedding.TextEmbedding3Large` — `text-embedding-3-large`

## Supported Features

- `IJsonOutputFeature` — JSON Mode + Structured Outputs
- `IToolCallingFeature` — Function calling with strict mode
- `IStreamingChatFeature` — SSE streaming, `[DONE]` terminator
- Vision — Data URIs (`data:image/png;base64,...`)

## Reasoning Models

For o-series models, configure reasoning effort:

```csharp
services.AddOpenAiClient(o =>
{
    o.ApiKey = "sk-...";
    o.ReasoningEffort = ReasoningEffort.High;
});
```

## Responses API (GPT-5)

GPT-5 uses the Responses API automatically. Configure text verbosity:

```csharp
services.AddOpenAiClient(o =>
{
    o.ApiKey = "sk-...";
    o.TextVerbosity = TextVerbosity.Concise;
});
```

Streaming uses `response.completed` event instead of `[DONE]`.

## Embeddings

```csharp
var client = provider.GetRequiredService<IEmbeddingClient>();
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"],
    Model: OpenAiModels.Embedding.TextEmbedding3Small,
    Dimensions: 256)); // dimension reduction supported
```

## Troubleshooting

**Model not routing correctly:**
Check model name matches expected pattern. Use `IncludeRawResponse = true` to see the actual API called.

**Responses API errors:**
GPT-5 uses a different request format. Ensure latest Cisharpai.OpenAi package.
