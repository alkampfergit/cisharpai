using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureOpenAi.Models;

public sealed class AzureOpenAiEmbeddingData
{
    public string Object { get; set; } = string.Empty;

    /// <summary>
    /// The embedding value - can be a float array or a base64 string depending on encoding_format.
    /// </summary>
    public JsonElement Embedding { get; set; }

    public int Index { get; set; }
}

public sealed class AzureOpenAiEmbeddingUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public sealed class AzureOpenAiEmbeddingResponse
{
    public string Object { get; set; } = string.Empty;

    public List<AzureOpenAiEmbeddingData> Data { get; set; } = [];

    public string Model { get; set; } = string.Empty;

    public AzureOpenAiEmbeddingUsage Usage { get; set; } = new();
}
