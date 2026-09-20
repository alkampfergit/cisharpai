# Vision: Image Input in Chat Messages

Cisharpai supports sending images alongside text in chat messages. Images can be provided from local files or as raw base64 data.

## Quick Start

```csharp
// From a local file
var message = LlmMessage.WithImage("Describe this image", "/path/to/image.png");

// From base64-encoded data
var base64Data = Convert.ToBase64String(imageBytes);
var message = LlmMessage.WithBase64Image("What is in this image?", base64Data, "image/png");

var request = new ChatCompletionRequest(
    Messages: [message],
    Model: "gpt-4o");

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.Content);
```

## MessageContentPart Types

Vision messages use `ContentParts` instead of plain `Content`. Three content part types are available:

| Type | Description |
|------|-------------|
| `TextContentPart(string Text)` | A text segment within a multimodal message |
| `ImageFileContentPart(string FilePath)` | An image loaded from a local file path |
| `ImageBase64ContentPart(string Base64Data, string MediaType)` | An image provided as base64-encoded data |

### Composing Messages Manually

```csharp
var message = new LlmMessage(
    LlmRole.User,
    string.Empty,
    ContentParts:
    [
        new TextContentPart("What is shown in these two images?"),
        new ImageBase64ContentPart(base64Image1, "image/png"),
        new ImageBase64ContentPart(base64Image2, "image/jpeg")
    ]);
```

### Using Factory Methods

```csharp
// Convenience factory for text + one image file
var message = LlmMessage.WithImage("Describe this photo:", "/photos/sunset.jpg");

// Convenience factory for text + one base64 image
var message = LlmMessage.WithBase64Image(
    "What color is this object?",
    base64EncodedData,
    "image/png");
```

## Supported Image Formats

All providers that support vision accept: **PNG, JPEG, WebP, GIF**

## Provider Support

| Provider | Support | Notes |
|----------|---------|-------|
| OpenAI | Yes | Images sent as data URIs (`data:image/{mime};base64,...`) |
| Azure OpenAI | Yes | Same format as OpenAI |
| Azure AI Inference | Yes | Model-dependent (e.g., Phi-3-vision, Llama-3.2-vision) |
| Anthropic | Yes | Images sent as raw base64 in `source` object (NOT data URIs) |
| Cohere | Partial | Image parts silently skipped; only text content extracted |

## OpenAI Example

```csharp
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddOpenAiClient(options => { options.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!; });

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();

var message = LlmMessage.WithImage("What is in this image?", "/path/to/photo.jpg");
var request = new ChatCompletionRequest(
    Messages: [message],
    Model: "gpt-4o",
    MaxTokens: 500);

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.Content);
```

## Anthropic Example

```csharp
using Cisharpai.Anthropic;

services.AddAnthropicClient(options => { options.ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!; });
// Usage is identical — Cisharpai handles the format difference automatically
```

## Cohere Behavior

Cohere's chat API does not support image input. When a `LlmMessage` with image content parts is sent to Cohere:

- **Image parts are silently skipped** — only text parts are extracted and sent
- If a message has only image parts (no text), an empty string is sent as content
- No exception is thrown

For image-based vector search with Cohere, use `IImageEmbeddingFeature` or `IMultimodalEmbeddingFeature` instead.

## Loading Images from Files vs Base64

**File path** (`LlmMessage.WithImage` or `ImageFileContentPart`):
- The file is read from disk when the request is built
- Supports PNG, JPEG, WebP, GIF (MIME type detected from extension)
- Convenient for local development

**Base64** (`LlmMessage.WithBase64Image` or `ImageBase64ContentPart`):
- Data is embedded directly without file I/O at request time
- Useful when images come from memory, databases, or APIs
- Must specify the `mediaType` (e.g., `"image/png"`)

```csharp
// File path approach
var imageBytes = await File.ReadAllBytesAsync("/path/to/image.png");
// ... or just use LlmMessage.WithImage("/path/to/image.png")

// Base64 approach
var imageBytes = await File.ReadAllBytesAsync("/path/to/image.png");
var base64 = Convert.ToBase64String(imageBytes);
var message = LlmMessage.WithBase64Image("Describe:", base64, "image/png");
```

## See Also

- [Provider Features Matrix](provider-features.md) — full feature support table
- [Embeddings](embeddings.md) — for image vector embeddings (Cohere, Azure AI Inference)
