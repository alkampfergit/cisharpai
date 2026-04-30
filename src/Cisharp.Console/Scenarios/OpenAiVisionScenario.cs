using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class OpenAiVisionScenario : IScenario
{
    public string Name => "OpenAI vision";
    public string Description => "Sends an image to OpenAI and asks it to describe the image.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.OpenAiApiKey);

        var model = await AnsiConsole.AskAsync("Model?", "gpt-4o", cancellationToken);
        var imagePath = await AnsiConsole.AskAsync<string>("Image file path (local file)?", cancellationToken);
        var prompt = await AnsiConsole.AskAsync("Prompt?", "Describe this image in detail.", cancellationToken);

        if (!File.Exists(imagePath))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {Markup.Escape(imagePath)}");
            return;
        }

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddOpenAiClient(options => { options.ApiKey = apiKey; });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var message = LlmMessage.WithImage(prompt, imagePath);

        var request = new ChatCompletionRequest(
            Messages: [message],
            Model: model,
            MaxTokens: 1000);

        AnsiConsole.MarkupLine("[grey]Sending image to OpenAI...[/]");

        var response = await client.GetChatCompletionAsync(request, cancellationToken);

        if (!response.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(response.ErrorMessage ?? "Unknown error")}");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Model:[/] {Markup.Escape(response.Model)}");
        AnsiConsole.MarkupLine($"[yellow]Tokens:[/] prompt {response.PromptTokens}, completion {response.CompletionTokens}");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine(response.Content);
    }
}
