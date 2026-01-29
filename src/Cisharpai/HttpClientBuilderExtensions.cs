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

            resilienceOptions.TotalRequestTimeout = new HttpTimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(90)
            };

            resilienceOptions.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                FailureRatio = 0.2,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(15)
            };
        });

        return builder;
    }
}
