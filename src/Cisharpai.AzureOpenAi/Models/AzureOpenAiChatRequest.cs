using System.Text.Json.Serialization;

namespace Cisharpai.AzureOpenAi.Models;

public sealed class AzureOpenAiChatMessage
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public sealed class AzureOpenAiChatRequest
{
    public List<AzureOpenAiChatMessage> Messages { get; set; } = [];

    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
}
