using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Azure;
using Cisharpai.Azure.AzureOpenAi;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class AzureOpenAiChatScenario : IScenario
{
    public string Name => "Azure OpenAI chat";
    public string Description => "Runs a chat completion using Azure OpenAI.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = ScenarioHelpers.RequireEnv(DotEnv.AzureOpenAiEndpoint);
        var deployment = ScenarioHelpers.RequireEnv(DotEnv.AzureOpenAiDeployments);
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.AzureOpenAiApiKey);
        var apiVersion = ScenarioHelpers.GetEnv(DotEnv.AzureOpenAiApiVersion);

        var prompt = AnsiConsole.Ask("User prompt?", "Summarize the benefits of dependency injection.");

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddAzureOpenAiClient(options =>
        {
            options.Endpoint = endpoint;
            options.DeploymentName = deployment;
            options.ApiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(apiVersion))
                options.ApiVersion = apiVersion;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, prompt)],
            Model: deployment,
            Temperature: 0.2,
            MaxTokens: 200);

        var response = await client.GetChatCompletionAsync(request, cancellationToken);

        AnsiConsole.MarkupLine($"[green]Model:[/] {Markup.Escape(response.Model)}");
        AnsiConsole.MarkupLine($"[yellow]Tokens:[/] prompt {response.PromptTokens}, completion {response.CompletionTokens}");
        AnsiConsole.WriteLine(response.Content);
    }
}
