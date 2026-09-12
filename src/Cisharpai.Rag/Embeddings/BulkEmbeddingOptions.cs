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
        ValidateOptions(MaxBatchItems, Dimensions, InputType, MaxBatchTokens, TokenEstimator, MaxConcurrency, MaxRetries, RetryBaseDelay);

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

    private static void ValidateOptions(
        int maxBatchItems, int? dimensions, EmbeddingInputType inputType,
        int? maxBatchTokens, Func<string, int>? tokenEstimator,
        int maxConcurrency, int maxRetries, TimeSpan retryBaseDelay)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatchItems);
        if (dimensions is <= 0)
            throw new ArgumentOutOfRangeException(nameof(dimensions), dimensions, "Dimensions must be positive when specified.");
        if (!Enum.IsDefined(inputType))
            throw new ArgumentOutOfRangeException(nameof(inputType), inputType, "Unknown embedding input type.");
        if (maxBatchTokens is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBatchTokens), maxBatchTokens, "MaxBatchTokens must be positive when specified.");
        if (maxBatchTokens is not null && tokenEstimator is null)
            throw new ArgumentException("TokenEstimator must not be null when MaxBatchTokens is set.", nameof(tokenEstimator));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);
        if (maxConcurrency > 32)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "MaxConcurrency must not exceed 32.");
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
        if (retryBaseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryBaseDelay), retryBaseDelay, "RetryBaseDelay must be positive.");
    }
}
