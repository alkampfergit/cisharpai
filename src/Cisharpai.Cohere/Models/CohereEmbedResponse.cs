using System.Text.Json.Serialization;

namespace Cisharpai.Cohere.Models;

public sealed class CohereEmbeddings
{
    public List<List<float>>? Float { get; set; }

    public List<List<sbyte>>? Int8 { get; set; }

    public List<string>? Binary { get; set; }
}

public sealed class CohereBilledUnits
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("images")]
    public int? Images { get; set; }

    [JsonPropertyName("image_tokens")]
    public int? ImageTokens { get; set; }
}

public sealed class CohereMeta
{
    [JsonPropertyName("billed_units")]
    public CohereBilledUnits BilledUnits { get; set; } = new();
}

public sealed class CohereImageMetadata
{
    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? Format { get; set; }

    [JsonPropertyName("bit_depth")]
    public int? BitDepth { get; set; }
}

public sealed class CohereEmbedResponse
{
    public string Id { get; set; } = string.Empty;

    public CohereEmbeddings Embeddings { get; set; } = new();

    public List<string> Texts { get; set; } = [];

    [JsonPropertyName("images")]
    public List<CohereImageMetadata>? ResponseImages { get; set; }

    public CohereMeta Meta { get; set; } = new();
}
