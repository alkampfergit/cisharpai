# Vision (Image Input)

## Factory Methods

```csharp
// From file path (MIME type auto-detected)
var msg = LlmMessage.WithImage("Describe this image", "photo.png");

// From base64 data
var msg = LlmMessage.WithBase64Image("Describe this", base64Data, "image/png");
```

## ContentPart Types

- `TextContentPart(string Text)` — Text segment
- `ImageFileContentPart(string FilePath)` — Image loaded from file at request time
- `ImageBase64ContentPart(string Base64Data, string MediaType)` — Embedded base64

## Multi-Part Messages

```csharp
var msg = new LlmMessage("user")
{
    ContentParts =
    [
        new TextContentPart("Compare these two images:"),
        new ImageFileContentPart("before.png"),
        new ImageFileContentPart("after.png")
    ]
};
```

## Supported Formats

PNG, JPEG, WebP, GIF — all providers.

## Provider Image Handling

| Provider | Format Sent | Notes |
|----------|------------|-------|
| OpenAI | Data URI (`data:image/png;base64,...`) | Standard |
| Azure OpenAI | Data URI | Same as OpenAI |
| Azure AI Inference | Data URI | Model-dependent support |
| Anthropic | Raw base64 in `source` object | NOT data URIs |
| Cohere | N/A | Image parts silently skipped |

Cisharpai handles all format conversions automatically. Use the same code for all providers.

## Image Loading

- **File path:** File read at request time. MIME type detected from extension.
- **Base64:** Embedded directly. Use for images from memory, databases, or APIs.

## Limitations

- **Cohere:** Vision is partial — image content parts are silently dropped, only text is sent.
- **Azure AI Inference:** Vision support depends on deployed model (Phi-3-vision, Llama-3.2-vision).
