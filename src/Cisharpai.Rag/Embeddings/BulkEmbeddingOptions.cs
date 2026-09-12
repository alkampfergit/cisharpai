using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Rag.Embeddings;

/// <summary>Provider request settings for bounded embedding batches with optional concurrency and retry.</summary>
public sealed class BulkEmbeddingOptions
{
    public int MaxBatchItems { get; set; } = 32;
    public int? MaxBatchTokens { get; set; }
    public Func<string, int>? TokenEstimator { get; set; } = s => s.Length / 4;
    public int MaxConcurrency { get; set; } = 1;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public Func<EmbeddingResponse, bool>? IsTransientError { get; set; }
    public string? Model { get; set; }
    public EmbeddingInputType InputType { get; set; } = EmbeddingInputType.Document;
    public int? Dimensions { get; set; }
    public bool IncludeRawResponse { get; set; }
    public JsonElement? ExtraParameters { get; set; }

    internal BulkEmbeddingOptions Snapshot()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxBatchItems);
        if (Dimensions is <= 0)
            throw new ArgumentOutOfRangeException(nameof(Dimensions), Dimensions, "Dimensions must be positive when specified.");
        if (!Enum.IsDefined(InputType))
            throw new ArgumentOutOfRangeException(nameof(InputType), InputType, "Unknown embedding input type.");
        if (MaxBatchTokens is <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxBatchTokens), MaxBatchTokens, "MaxBatchTokens must be positive when specified.");
        if (MaxBatchTokens is not null && TokenEstimator is null)
            throw new ArgumentException("TokenEstimator must not be null when MaxBatchTokens is set.", nameof(TokenEstimator));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConcurrency);
        if (MaxConcurrency > 32)
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrency), MaxConcurrency, "MaxConcurrency must not exceed 32.");
        ArgumentOutOfRangeException.ThrowIfNegative(MaxRetries);
        if (RetryBaseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RetryBaseDelay), RetryBaseDelay, "RetryBaseDelay must be positive.");

        return new BulkEmbeddingOptions
        {
            MaxBatchItems = MaxBatchItems,
            MaxBatchTokens = MaxBatchTokens,
            TokenEstimator = TokenEstimator,
            MaxConcurrency = MaxConcurrency,
            MaxRetries = MaxRetries,
            RetryBaseDelay = RetryBaseDelay,
            IsTransientError = IsTransientError,
            Model = Model,
            InputType = InputType,
            Dimensions = Dimensions,
            IncludeRawResponse = IncludeRawResponse,
            ExtraParameters = ExtraParameters?.Clone()
        };
    }
}
