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

    /// <summary>
    /// Maximum number of dispatched batches that may be awaiting in-order delivery to the caller.
    /// Bounds the memory held by the reordering window when an early batch is slow or the consumer
    /// is slow. Defaults to <c>MaxConcurrency * 2</c> when null. Ignored when <see cref="MaxConcurrency"/> is 1.
    /// </summary>
    public int? MaxPendingBatches { get; set; }

    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public Func<EmbeddingResponse, bool>? IsTransientError { get; set; }
    public string? Model { get; set; }
    public EmbeddingInputType InputType { get; set; } = EmbeddingInputType.Document;
    public int? Dimensions { get; set; }
    public bool IncludeRawResponse { get; set; }
    public JsonElement? ExtraParameters { get; set; }

    /// <summary>Effective reordering window: <see cref="MaxPendingBatches"/> when set, otherwise <c>MaxConcurrency * 2</c>.</summary>
    internal int EffectiveMaxPendingBatches => MaxPendingBatches ?? MaxConcurrency * 2;

    /// <summary>Creates options preconfigured with the batch ceilings of <paramref name="profile"/>.</summary>
    public static BulkEmbeddingOptions ForProvider(EmbeddingProviderProfile profile) =>
        new BulkEmbeddingOptions().ApplyProfile(profile);

    /// <summary>
    /// Overwrites <see cref="MaxBatchItems"/> and <see cref="MaxBatchTokens"/> with the ceilings of
    /// <paramref name="profile"/>, leaving every other property untouched. Apply this before any
    /// explicit batch-sizing override, otherwise the profile replaces it.
    /// </summary>
    public BulkEmbeddingOptions ApplyProfile(EmbeddingProviderProfile profile)
    {
        (MaxBatchItems, MaxBatchTokens) = profile switch
        {
            EmbeddingProviderProfile.Conservative => (32, (int?)null),
            EmbeddingProviderProfile.OpenAi => (96, 250_000),
            EmbeddingProviderProfile.AzureOpenAi => (16, 100_000),
            EmbeddingProviderProfile.AzureAiInference => (64, 100_000),
            EmbeddingProviderProfile.Cohere => (96, 100_000),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown embedding provider profile.")
        };
        return this;
    }

    internal BulkEmbeddingOptions Snapshot()
    {
        ValidateOptions(MaxBatchItems, Dimensions, InputType, MaxBatchTokens, TokenEstimator, MaxConcurrency, MaxPendingBatches, MaxRetries, RetryBaseDelay);

        return new BulkEmbeddingOptions
        {
            MaxBatchItems = MaxBatchItems,
            MaxBatchTokens = MaxBatchTokens,
            TokenEstimator = TokenEstimator,
            MaxConcurrency = MaxConcurrency,
            MaxPendingBatches = MaxPendingBatches,
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
        int maxConcurrency, int? maxPendingBatches, int maxRetries, TimeSpan retryBaseDelay)
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
        if (maxPendingBatches is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPendingBatches), maxPendingBatches, "MaxPendingBatches must be positive when specified.");
        if (maxPendingBatches is not null && maxPendingBatches < maxConcurrency)
            throw new ArgumentOutOfRangeException(nameof(maxPendingBatches), maxPendingBatches, "MaxPendingBatches must be greater than or equal to MaxConcurrency.");
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
        if (retryBaseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryBaseDelay), retryBaseDelay, "RetryBaseDelay must be positive.");
    }
}
