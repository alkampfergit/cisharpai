using System.Text.Json;

namespace Cisharpai.Cohere.Models;

public sealed class CohereChatResponseFormat
{
    public string Type { get; set; } = string.Empty;
    public JsonElement? JsonSchema { get; set; }
}
