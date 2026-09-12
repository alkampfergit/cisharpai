using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiEmbeddingData
{
    public string Object { get; set; } = string.Empty;

    /// <summary>
    /// The embedding value - can be a float array or a base64 string depending on encoding_format.
    /// </summary>
    public JsonElement Embedding { get; set; }

    public int Index { get; set; }
}

public sealed class OpenAiEmbeddingUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public sealed class OpenAiEmbeddingResponse
{
    public string Object { get; set; } = string.Empty;

    public List<OpenAiEmbeddingData> Data { get; set; } = [];

    public string Model { get; set; } = string.Empty;

    public OpenAiEmbeddingUsage Usage { get; set; } = new();
}
