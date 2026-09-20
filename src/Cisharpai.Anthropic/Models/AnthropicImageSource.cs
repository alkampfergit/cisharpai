using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

/// <summary>
/// The source of an image in an Anthropic vision message content block.
/// </summary>
public sealed class AnthropicImageSource
{
    /// <summary>Always "base64" for base64-encoded images.</summary>
    public string Type { get; set; } = "base64";

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Base64-encoded image data (raw, without data URI prefix).</summary>
    public string Data { get; set; } = string.Empty;
}
