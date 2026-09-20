using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class CohereRerankScenario : IScenario
{
    public string Name => "Cohere rerank";
    public string Description => "Reranks a set of documents against a query using Cohere.";

    private static readonly string[] SampleDocuments =
    [
        "Carson City is the capital city of the American state of Nevada.",
        "Paris is the capital and most populous city of France.",
        "The Louvre is the world's most-visited museum, located in Paris.",
        "Washington, D.C. is the capital of the United States."
    ];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.CohereApiKey);

        var model = await AnsiConsole.AskAsync("Model?", CohereModels.Rerank.RerankV3_5, cancellationToken);
        var query = await AnsiConsole.AskAsync("Query?", "What is the capital of France?", cancellationToken);

        var documents = new List<string>();
        if (await AnsiConsole.ConfirmAsync("Use the sample documents?", true, cancellationToken))
        {
            documents.AddRange(SampleDocuments);
        }
        else
        {
            while (true)
            {
                var document = await AnsiConsole.AskAsync("Document (blank to finish)?", string.Empty, cancellationToken);
                if (string.IsNullOrWhiteSpace(document))
                    break;
                documents.Add(document);
            }

            if (documents.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No documents provided.[/]");
                return;
            }
        }

        var topN = await AnsiConsole.AskAsync("Top N?", documents.Count, cancellationToken);

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddCohereRerankerClient(options =>
        {
            options.ApiKey = apiKey;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IRerankerClient>();

        var request = new RerankRequest(
            Query: query,
            Documents: documents,
            Model: model,
            TopN: topN);

        var response = await client.RerankAsync(request, cancellationToken);

        if (!response.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(response.ErrorMessage ?? "Unknown error")}");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Model:[/] {Markup.Escape(response.Model)}");
        if (response.SearchUnits is not null)
            AnsiConsole.MarkupLine($"[yellow]Search units:[/] {response.SearchUnits}");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Rank");
        table.AddColumn("Score");
        table.AddColumn("Document");

        var rank = 1;
        foreach (var result in response.Results)
        {
            table.AddRow(
                rank.ToString(),
                result.RelevanceScore.ToString("F6"),
                Markup.Escape(documents[result.Index]));
            rank++;
        }

        AnsiConsole.Write(table);
    }
}
