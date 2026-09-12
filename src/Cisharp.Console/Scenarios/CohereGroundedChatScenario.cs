using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Cohere;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class CohereGroundedChatScenario : IScenario
{
    public string Name => "Cohere grounded chat (RAG)";
    public string Description => "Runs a grounded chat completion with documents and citations using Cohere.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.CohereApiKey);

        var model = AnsiConsole.Ask("Model?", "command-a-03-2025");

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddCohereChatClient(options =>
        {
            options.ApiKey = apiKey;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();
        var groundedFeature = client.Features.Get<IGroundedChatFeature>()!;

        var documents = new List<DocumentChunk>
        {
            new(Id: "doc-1", Data: new Dictionary<string, string>
            {
                ["title"] = "France",
                ["snippet"] = "Paris is the capital city of France. It is located in northern France on the Seine river. The Eiffel Tower is one of the most famous landmarks in Paris."
            }),
            new(Id: "doc-2", Data: new Dictionary<string, string>
            {
                ["title"] = "Italy",
                ["snippet"] = "Rome is the capital city of Italy. It is known for the Colosseum, the Vatican, and its rich history dating back to the Roman Empire."
            }),
            new(Id: "doc-3", Data: new Dictionary<string, string>
            {
                ["title"] = "Japan",
                ["snippet"] = "Tokyo is the capital city of Japan. It is the most populous metropolitan area in the world and a global center for technology and culture."
            })
        };

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What are the capitals of France, Italy, and Japan? What is each city known for?")],
            Model: model,
            Temperature: 0,
            MaxTokens: 500);

        var options = new GroundedChatOptions(
            Documents: documents,
            CitationMode: CitationMode.Accurate);

        AnsiConsole.MarkupLine("[blue]Sending grounded chat request with 3 documents...[/]");

        var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options, cancellationToken);

        if (!response.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(response.ErrorMessage ?? "Unknown error")}");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Model:[/] {Markup.Escape(response.ChatCompletion.Model)}");
        AnsiConsole.MarkupLine($"[yellow]Tokens:[/] prompt {response.ChatCompletion.PromptTokens}, completion {response.ChatCompletion.CompletionTokens}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Response:[/]");
        AnsiConsole.WriteLine(response.Content);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Citations ({response.Citations.Count}):[/]");
        foreach (var citation in response.Citations)
        {
            AnsiConsole.MarkupLine($"  [{citation.Start}..{citation.End}] \"{Markup.Escape(citation.Text)}\"");
            foreach (var source in citation.Sources)
            {
                var docInfo = source.Data is not null ? string.Join(", ", source.Data.Select(kv => $"{kv.Key}: {kv.Value}")) : "";
                AnsiConsole.MarkupLine($"    [dim]Source:[/] {Markup.Escape(source.Id)} {Markup.Escape(docInfo)}");
            }
        }
    }
}
