namespace Cisharpai.Models;

/// <summary>
/// A part of a multimodal message. Used in <see cref="LlmMessage.ContentParts"/>.
/// </summary>
public abstract record MessageContentPart;

/// <summary>Text content in a multimodal message.</summary>
public sealed record TextContentPart(string Text) : MessageContentPart;

/// <summary>Image from a local file path. Automatically converted to base64 by the provider client.</summary>
public sealed record ImageFileContentPart(string FilePath) : MessageContentPart;

/// <summary>Image from raw base64 data with explicit MIME type.</summary>
public sealed record ImageBase64ContentPart(
    string Base64Data,
    string MediaType) : MessageContentPart;
