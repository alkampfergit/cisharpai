using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Azure;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class AzureAiInferenceChatScenario : IScenario
{
    public string Name => "Azure AI Inference chat";
    public string Description => "Runs a chat completion using Azure AI Inference (Phi-3, Llama-3, Mistral, etc.).";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = ScenarioHelpers.RequireEnv(DotEnv.AzureInferenceEndpoint);
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.AzureInferenceApiKey);
        var modelId = ScenarioHelpers.RequireEnv(DotEnv.AzureInferenceModel);

        var prompt = AnsiConsole.Ask("User prompt?", "Explain the benefits of dependency injection in 3 sentences.");

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddAzureAiInferenceChatCompletion(options =>
        {
            options.Endpoint = endpoint;
            options.ApiKey = apiKey;
            options.ModelId = modelId;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, prompt)],
            Model: modelId,
            Temperature: 0.2,
            MaxTokens: 200);

        AnsiConsole.MarkupLine("[grey]Sending request...[/]");
        var response = await client.GetChatCompletionAsync(request, cancellationToken);

        if (!response.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(response.ErrorMessage ?? "Unknown error")}");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Model:[/] {Markup.Escape(response.Model)}");
        AnsiConsole.MarkupLine($"[yellow]Tokens:[/] prompt {response.PromptTokens}, completion {response.CompletionTokens}");
        AnsiConsole.WriteLine(response.Content);
    }
}
