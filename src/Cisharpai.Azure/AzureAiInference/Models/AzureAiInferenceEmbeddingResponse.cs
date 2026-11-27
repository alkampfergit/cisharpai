using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureAiInference.Models;

[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Property name mirrors the Azure AI Inference API JSON schema")]
public sealed class AzureAiInferenceEmbeddingData
{
    public int Index { get; set; }

    public string Object { get; set; } = "embedding";

    /// <summary>
    /// The embedding vector. Can be float[] or base64 string depending on encoding_format.
    /// </summary>
    public JsonElement Embedding { get; set; }
}

public sealed class AzureAiInferenceEmbeddingUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Property name mirrors the Azure AI Inference API JSON schema")]
public sealed class AzureAiInferenceEmbeddingResponse
{
    public string Id { get; set; } = string.Empty;

    public string Object { get; set; } = "list";

    public string Model { get; set; } = string.Empty;

    public List<AzureAiInferenceEmbeddingData> Data { get; set; } = [];

    public AzureAiInferenceEmbeddingUsage? Usage { get; set; }
}
