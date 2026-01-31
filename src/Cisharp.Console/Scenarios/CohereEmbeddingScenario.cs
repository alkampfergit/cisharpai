using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class CohereEmbeddingScenario : IScenario
{
    public string Name => "Cohere embedding";
    public string Description => "Gets text embeddings using Cohere.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.CohereApiKey);

        var model = AnsiConsole.Ask("Model?", "embed-english-v3.0");
        var text = AnsiConsole.Ask("Text to embed?", "The quick brown fox jumps over the lazy dog.");

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddCohereEmbeddingClient(options =>
        {
            options.ApiKey = apiKey;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var request = new EmbeddingRequest(
            Input: [text],
            Model: model,
            InputType: EmbeddingInputType.Document);

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
