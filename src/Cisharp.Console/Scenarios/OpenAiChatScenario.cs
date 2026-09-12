using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class OpenAiChatScenario : IScenario
{
    public string Name => "OpenAI chat";
    public string Description => "Runs a chat completion using OpenAI.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.OpenAiApiKey);
        var baseUrl = ScenarioHelpers.GetEnv(DotEnv.OpenAiBaseUrl);
        var organization = ScenarioHelpers.GetEnv(DotEnv.OpenAiOrganization);

        var model = AnsiConsole.Ask("Model?", "gpt-4.1-nano");
        var prompt = AnsiConsole.Ask("User prompt?", "Write a short haiku about winter.");

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddOpenAiClient(options =>
        {
            options.ApiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(baseUrl))
                options.BaseUrl = baseUrl;
            if (!string.IsNullOrWhiteSpace(organization))
                options.Organization = organization;
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
