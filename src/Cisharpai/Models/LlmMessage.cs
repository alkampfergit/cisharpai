namespace Cisharpai.Models;

/// <summary>
/// Represents a message in a conversation.
/// </summary>
public sealed record LlmMessage(
    LlmRole Role,
    string Content,

    /// <summary>
    /// For role=Tool messages: identifies which tool call this message responds to.
    /// </summary>
    string? ToolCallId = null,

    /// <summary>
    /// For role=Assistant messages: tool invocations requested by the model.
    /// </summary>
    IReadOnlyList<ToolCall>? ToolCalls = null,

    /// <summary>
    /// Multimodal content parts. When non-null, providers use this instead of <see cref="Content"/>
    /// for building the request. Text-only callers continue using <see cref="Content"/> as before.
    /// </summary>
    IReadOnlyList<MessageContentPart>? ContentParts = null)
{
    /// <summary>
    /// Creates a user message with a text prompt and a local image file.
    /// </summary>
    public static LlmMessage WithImage(string text, string imagePath) =>
        new(LlmRole.User, string.Empty, ContentParts: new MessageContentPart[]
        {
            new TextContentPart(text),
            new ImageFileContentPart(imagePath)
        });

    /// <summary>
    /// Creates a user message with a text prompt and a base64-encoded image.
    /// </summary>
    public static LlmMessage WithBase64Image(string text, string base64Data, string mediaType) =>
        new(LlmRole.User, string.Empty, ContentParts: new MessageContentPart[]
        {
            new TextContentPart(text),
            new ImageBase64ContentPart(base64Data, mediaType)
        });
}
