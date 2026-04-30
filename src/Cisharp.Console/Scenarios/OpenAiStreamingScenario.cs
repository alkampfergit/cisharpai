using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class OpenAiStreamingScenario : IScenario
{
    public string Name => "OpenAI streaming";
    public string Description => "Streams a chat completion from OpenAI, showing tokens in real time.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.OpenAiApiKey);
        var baseUrl = ScenarioHelpers.GetEnv(DotEnv.OpenAiBaseUrl);

        var model = await AnsiConsole.AskAsync("Model?", "gpt-4.1-nano", cancellationToken);
        var prompt = await AnsiConsole.AskAsync("User prompt?", "Write a short poem about the ocean.", cancellationToken);

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddOpenAiClient(options =>
        {
            options.ApiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(baseUrl))
                options.BaseUrl = baseUrl;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var streamFeature = client.Features.Get<IStreamingChatFeature>();
        if (streamFeature is null)
        {
            AnsiConsole.MarkupLine("[red]Streaming is not supported by this client.[/]");
            return;
        }

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, prompt)],
            Model: model,
            Temperature: 0.7,
            MaxTokens: 500);

        AnsiConsole.MarkupLine("[grey]Streaming response...[/]");
        AnsiConsole.WriteLine();

        int? promptTokens = null;
        int? completionTokens = null;
        string? finishReason = null;

        await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                System.Console.Write(chunk.Content);
            }

            if (chunk.FinishReason is not null)
                finishReason = chunk.FinishReason;

            if (chunk.PromptTokens.HasValue)
                promptTokens = chunk.PromptTokens;

            if (chunk.CompletionTokens.HasValue)
                completionTokens = chunk.CompletionTokens;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Finish reason:[/] {Markup.Escape(finishReason ?? "unknown")}");
        if (promptTokens.HasValue || completionTokens.HasValue)
        {
            AnsiConsole.MarkupLine($"[yellow]Tokens:[/] prompt {promptTokens}, completion {completionTokens}");
        }
    }
}
