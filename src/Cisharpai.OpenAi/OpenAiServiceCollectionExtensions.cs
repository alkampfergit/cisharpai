using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Cisharpai.OpenAi;

public static class OpenAiServiceCollectionExtensions
{
    public static IHttpClientBuilder AddOpenAiClient(
        this IServiceCollection services,
        Action<OpenAiClientOptions> configure)
    {
        var options = new OpenAiClientOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddTransient<OpenAiAuthenticationHandler>();

        var builder = services.AddHttpClient<OpenAiChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler<OpenAiAuthenticationHandler>();

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

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<OpenAiChatCompletionClient>());

        return builder;
    }
}
