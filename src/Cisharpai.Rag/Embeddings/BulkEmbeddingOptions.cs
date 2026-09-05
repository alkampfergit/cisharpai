using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Rag.Embeddings;

/// <summary>Provider request settings for sequential bounded embedding batches.</summary>
public sealed class BulkEmbeddingOptions
{
    public int BatchSize { get; set; } = 32;
    public string? Model { get; set; }
    public EmbeddingInputType InputType { get; set; } = EmbeddingInputType.Document;
    public int? Dimensions { get; set; }
    public bool IncludeRawResponse { get; set; }
    public JsonElement? ExtraParameters { get; set; }

    internal BulkEmbeddingOptions Snapshot()
    {
        if (BatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(BatchSize), "Batch size must be positive.");
        if (Dimensions is <= 0) throw new ArgumentOutOfRangeException(nameof(Dimensions), "Dimensions must be positive when specified.");
        if (!Enum.IsDefined(InputType)) throw new ArgumentOutOfRangeException(nameof(InputType), "Unknown embedding input type.");
        return new BulkEmbeddingOptions
        {
            BatchSize = BatchSize,
            Model = Model,
            InputType = InputType,
            Dimensions = Dimensions,
            IncludeRawResponse = IncludeRawResponse,
            ExtraParameters = ExtraParameters?.Clone()
        };
    }
}
