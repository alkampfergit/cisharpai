using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Cisharpai;

public static class HttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddCisharpaiResilienceHandler(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(resilienceOptions =>
        {
            resilienceOptions.Retry = new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            };

            resilienceOptions.AttemptTimeout = new HttpTimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            resilienceOptions.TotalRequestTimeout = new HttpTimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(90)
            };

            resilienceOptions.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(120),
                FailureRatio = 0.2,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(15)
            };
        });

        return builder;
    }

    /// <summary>
    /// Adds a resilience handler optimized for streaming (SSE) requests.
    /// Disables per-attempt and total request timeouts to prevent cutting off
    /// long-running streams, while keeping retry and circuit breaker at default settings.
    /// </summary>
    /// <remarks>
    /// Use this handler for HttpClients that will be used with <see cref="Features.Chat.IStreamingChatFeature"/>.
    /// The standard <see cref="AddCisharpaiResilienceHandler"/> sets 60s/90s timeouts which
    /// would terminate any stream running longer than those limits.
    /// </remarks>
    public static IHttpClientBuilder AddCisharpaiStreamingResilienceHandler(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout = new HttpTimeoutStrategyOptions
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            options.TotalRequestTimeout = new HttpTimeoutStrategyOptions
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            // Retry and circuit breaker remain at default settings
        });
        return builder;
    }
}
