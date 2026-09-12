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

    internal BulkEmbeddingOptions Snapshot() => ValidateAndClone(BatchSize, Dimensions, InputType);

    private BulkEmbeddingOptions ValidateAndClone(int batchSize, int? dimensions, EmbeddingInputType inputType)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        if (dimensions is <= 0)
            throw new ArgumentOutOfRangeException(nameof(dimensions), dimensions, "Dimensions must be positive when specified.");
        if (!Enum.IsDefined(inputType))
            throw new ArgumentOutOfRangeException(nameof(inputType), inputType, "Unknown embedding input type.");
        return new BulkEmbeddingOptions
        {
            BatchSize = batchSize,
            Model = Model,
            InputType = inputType,
            Dimensions = dimensions,
            IncludeRawResponse = IncludeRawResponse,
            ExtraParameters = ExtraParameters?.Clone()
        };
    }
}
