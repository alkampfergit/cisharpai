using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Cohere;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class CohereChatScenario : IScenario
{
    public string Name => "Cohere chat";
    public string Description => "Runs a chat completion using Cohere.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.CohereApiKey);

        var model = AnsiConsole.Ask("Model?", "command-a-03-2025");
        var prompt = AnsiConsole.Ask("User prompt?", "Explain async/await in 3 sentences.");

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddCohereChatClient(options =>
        {
            options.ApiKey = apiKey;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, prompt)],
            Model: model,
            Temperature: 0.2,
            MaxTokens: 200);

        var response = await client.GetChatCompletionAsync(request, cancellationToken);

        AnsiConsole.MarkupLine($"[green]Model:[/] {Markup.Escape(response.Model)}");
        AnsiConsole.MarkupLine($"[yellow]Tokens:[/] prompt {response.PromptTokens}, completion {response.CompletionTokens}");
        AnsiConsole.WriteLine(response.Content);
    }
}
