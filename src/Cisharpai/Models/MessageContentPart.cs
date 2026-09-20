namespace Cisharpai.Models;

/// <summary>
/// Base type for discriminated-union content parts in a multimodal message.
/// Sealed subtypes: <see cref="TextContentPart"/>, <see cref="ImageFileContentPart"/>,
/// <see cref="ImageBase64ContentPart"/>. Used in <see cref="LlmMessage.ContentParts"/>.
/// </summary>
#pragma warning disable S2094 // Classes should not be empty — intentional discriminated-union base
public abstract record MessageContentPart;
#pragma warning restore S2094

/// <summary>Text content in a multimodal message.</summary>
public sealed record TextContentPart(string Text) : MessageContentPart;

/// <summary>Image from a local file path. Automatically converted to base64 by the provider client.</summary>
public sealed record ImageFileContentPart(string FilePath) : MessageContentPart;

/// <summary>Image from raw base64 data with explicit MIME type.</summary>
public sealed record ImageBase64ContentPart(
    string Base64Data,
    string MediaType) : MessageContentPart;
