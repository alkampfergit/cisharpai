using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class OpenAiEmbeddingScenario : IScenario
{
    public string Name => "OpenAI embedding";
    public string Description => "Gets text embeddings using OpenAI.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.OpenAiApiKey);
        var baseUrl = ScenarioHelpers.GetEnv(DotEnv.OpenAiBaseUrl);

        var model = AnsiConsole.Ask("Model?", "text-embedding-3-small");
        var text = AnsiConsole.Ask("Text to embed?", "The quick brown fox jumps over the lazy dog.");

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddOpenAiEmbeddingClient(options =>
        {
            options.ApiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(baseUrl))
                options.BaseUrl = baseUrl;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var request = new EmbeddingRequest(
            Input: [text],
            Model: model);

        var response = await client.GetEmbeddingsAsync(request, cancellationToken);

        if (!response.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(response.ErrorMessage ?? "Unknown error")}");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Model:[/] {Markup.Escape(response.Model)}");
        AnsiConsole.MarkupLine($"[yellow]Dimensions:[/] {response.Dimensions}");
        AnsiConsole.MarkupLine($"[yellow]Total tokens:[/] {response.TotalTokens}");
        AnsiConsole.MarkupLine($"[blue]First 10 values:[/] [{string.Join(", ", response.Embeddings[0].Take(10).Select(v => v.ToString("F6")))}]");
    }
}
