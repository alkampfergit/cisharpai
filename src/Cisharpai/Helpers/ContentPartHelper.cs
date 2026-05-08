using System.Text;
using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Helpers;

/// <summary>
/// Shared helper for extracting string content from provider response objects.
/// </summary>
public static class ContentPartHelper
{
    /// <summary>
    /// Extracts a string from a content value that may be a raw string, a JsonElement,
    /// or a structured array/object of text parts.
    /// Used by OpenAI, Azure OpenAI, and Azure AI Inference providers.
    /// </summary>
    public static string ExtractStringContent(object? content)
    {
        return content switch
        {
            string s => s,
            JsonElement je => ExtractStringContent(je),
            _ => string.Empty
        };
    }

    private static string ExtractStringContent(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => ExtractStringContentFromArray(content),
            JsonValueKind.Object => ExtractStringContentFromObject(content),
            _ => string.Empty
        };
    }

    private static string ExtractStringContentFromArray(JsonElement content)
    {
        var builder = new StringBuilder();

        foreach (var item in content.EnumerateArray())
        {
            builder.Append(ExtractStringContent(item));
        }

        return builder.ToString();
    }

    private static string ExtractStringContentFromObject(JsonElement content)
    {
        if (content.TryGetProperty("text", out var text))
        {
            return ExtractStringContent(text);
        }

        if (content.TryGetProperty("refusal", out var refusal))
        {
            return ExtractStringContent(refusal);
        }

        if (content.TryGetProperty("content", out var nestedContent))
        {
            return ExtractStringContent(nestedContent);
        }

        return string.Empty;
    }

    /// <summary>
    /// Maps <see cref="MessageContentPart"/> items to provider-specific content parts
    /// using the OpenAI-style image_url/data-URI format.
    /// Used by OpenAI, Azure OpenAI, and Azure AI Inference providers.
    /// </summary>
    public static async Task<List<T>> MapOpenAiStyleContentPartsAsync<T>(
        IReadOnlyList<MessageContentPart> contentParts,
        Func<string, T> createTextPart,
        Func<string, T> createImageUrlPart,
        CancellationToken ct)
    {
        var parts = new List<T>();
        foreach (var part in contentParts)
        {
            switch (part)
            {
                case TextContentPart text:
                    parts.Add(createTextPart(text.Text));
                    break;
                case ImageFileContentPart file:
                    var dataUri = await ImageDataUriHelper.ToDataUriAsync(file.FilePath, ct);
                    parts.Add(createImageUrlPart(dataUri));
                    break;
                case ImageBase64ContentPart base64:
                    parts.Add(createImageUrlPart(
                        $"data:{base64.MediaType};base64,{base64.Base64Data}"));
                    break;
            }
        }
        return parts;
    }
}
