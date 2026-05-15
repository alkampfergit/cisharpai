# Contract: Multimodal Messages (Vision)

**Source**: `src/Cisharpai/Models/MessageContentPart.cs`, `src/Cisharpai/Models/LlmMessage.cs`

## Public API

### Content Part Hierarchy

```csharp
public abstract record MessageContentPart;
public sealed record TextContentPart(string Text) : MessageContentPart;
public sealed record ImageFileContentPart(string FilePath) : MessageContentPart;
public sealed record ImageBase64ContentPart(string Base64Data, string MediaType) : MessageContentPart;
```

### LlmMessage Factory Methods

```csharp
public sealed record LlmMessage(...)
{
    public static LlmMessage WithImage(string text, string imagePath);
    public static LlmMessage WithBase64Image(string text, string base64Data, string mediaType);
}
```

### LlmMessage.ContentParts Property

```csharp
IReadOnlyList<MessageContentPart>? ContentParts = null
```

When `ContentParts` is non-null, providers use it instead of `Content` for building the request body.

## Behavioral Contract

### Input

Messages are created via factory methods or by populating `ContentParts` directly:

| Approach | When to use |
|----------|-------------|
| `LlmMessage.WithImage(text, filePath)` | Single local image file + text prompt |
| `LlmMessage.WithBase64Image(text, base64, mime)` | Single in-memory image + text prompt |
| Manual `ContentParts` | Multiple images, mixed sources, or custom ordering |

### Provider Behavior

| Provider | Image Format | Mapping Path |
|----------|-------------|-------------|
| OpenAI | `{type: "image_url", image_url: {url: "data:..."}}` | `ContentPartHelper.MapOpenAiStyleContentPartsAsync` |
| Azure OpenAI | Same as OpenAI | `ContentPartHelper.MapOpenAiStyleContentPartsAsync` |
| Azure AI Inference | Same as OpenAI | `ContentPartHelper.MapOpenAiStyleContentPartsAsync` |
| Anthropic | `{type: "image", source: {type: "base64", media_type, data}}` | Custom mapping in `AnthropicChatCompletionClient` |
| Cohere | Text-only extraction | Image parts silently skipped |

### Image File Processing

When a provider encounters an `ImageFileContentPart`:
1. `ImageDataUriHelper.ToDataUriAsync(filePath)` reads the file from disk
2. File bytes are base64-encoded
3. MIME type is detected from the file extension
4. Result is formatted as `data:{mime};base64,{base64data}`

For Anthropic, the base64 data and MIME type are extracted separately (no data URI wrapper).

### Error Conditions

| Condition | Behavior |
|-----------|----------|
| File does not exist | `FileNotFoundException` thrown (I/O error) |
| Unknown file extension | MIME defaults to `application/octet-stream` |
| Provider doesn't support images (Cohere) | Image parts silently skipped; only text extracted |
| Message has only image parts (no text) | Empty string sent as content for Cohere |

## Usage Examples

### Single Image from File

```csharp
var message = LlmMessage.WithImage("Describe this photo", "/photos/sunset.jpg");
var request = new ChatCompletionRequest(Messages: [message], Model: "gpt-4o");
var response = await client.GetChatCompletionAsync(request);
```

### Single Image from Base64

```csharp
var imageBytes = await File.ReadAllBytesAsync("/photos/chart.png");
var base64 = Convert.ToBase64String(imageBytes);
var message = LlmMessage.WithBase64Image("What data does this chart show?", base64, "image/png");
```

### Multiple Images

```csharp
var message = new LlmMessage(
    LlmRole.User,
    string.Empty,
    ContentParts:
    [
        new TextContentPart("Compare these two images:"),
        new ImageFileContentPart("/photos/before.jpg"),
        new ImageFileContentPart("/photos/after.jpg")
    ]);
```

### Cross-Provider Compatibility

```csharp
// Same message works on any provider
var message = LlmMessage.WithBase64Image("What is this?", base64Data, "image/png");
var request = new ChatCompletionRequest(Messages: [message]);

// OpenAI → data URI in image_url content part
// Anthropic → base64 source block
// Cohere → text-only fallback ("What is this?")
```
