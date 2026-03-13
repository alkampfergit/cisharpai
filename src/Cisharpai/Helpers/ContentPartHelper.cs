using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Helpers;

/// <summary>
/// Shared helper for extracting string content from provider response objects.
/// </summary>
public static class ContentPartHelper
{
    /// <summary>
    /// Extracts a string from a content value that may be a raw string or a JsonElement.
    /// Used by OpenAI, Azure OpenAI, and Azure AI Inference providers.
    /// </summary>
    public static string ExtractStringContent(object? content)
    {
        return content switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
            _ => string.Empty
        };
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
