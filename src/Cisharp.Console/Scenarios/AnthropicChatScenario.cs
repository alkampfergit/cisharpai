using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Anthropic;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class AnthropicChatScenario : IScenario
{
    public string Name => "Anthropic chat";
    public string Description => "Runs a chat completion using Anthropic.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.AnthropicApiKey);
        var baseUrl = ScenarioHelpers.GetEnv(DotEnv.AnthropicBaseUrl);
        var apiVersion = ScenarioHelpers.GetEnv(DotEnv.AnthropicApiVersion);

        var model = AnsiConsole.Ask("Model?", "claude-sonnet-4-20250514");
        var prompt = AnsiConsole.Ask("User prompt?", "Explain async/await in 3 sentences.");

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddAnthropicClient(options =>
        {
            options.ApiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(baseUrl))
                options.BaseUrl = baseUrl;
            if (!string.IsNullOrWhiteSpace(apiVersion))
                options.ApiVersion = apiVersion;
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
