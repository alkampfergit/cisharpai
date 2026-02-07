using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class OpenAiJsonOutputScenario : IScenario
{
    public string Name => "OpenAI JSON output";
    public string Description => "Demonstrates JSON Mode and Structured Outputs using OpenAI.";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ScenarioHelpers.RequireEnv(DotEnv.OpenAiApiKey);
        var baseUrl = ScenarioHelpers.GetEnv(DotEnv.OpenAiBaseUrl);

        var model = AnsiConsole.Ask("Model?", "gpt-4.1-nano");

        var services = ScenarioHelpers.CreateServiceCollection();
        services.AddOpenAiClient(options =>
        {
            options.ApiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(baseUrl))
                options.BaseUrl = baseUrl;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var jsonFeature = client.Features.Get<IJsonOutputFeature>();
        if (jsonFeature is null)
        {
            AnsiConsole.MarkupLine("[red]IJsonOutputFeature not available on this client.[/]");
            return;
        }

        // --- JSON Mode ---
        AnsiConsole.MarkupLine("[bold underline]1. JSON Mode[/]");

        var jsonModeRequest = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "List 3 colors with their hex codes")],
            Model: model,
            Temperature: 0,
            MaxTokens: 500);

        var jsonModeOptions = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var jsonModeResponse = await jsonFeature.GetChatCompletionWithJsonOutputAsync(
            jsonModeRequest, jsonModeOptions, cancellationToken);

        if (jsonModeResponse.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[green]Model:[/] {Markup.Escape(jsonModeResponse.Model)}");
            AnsiConsole.MarkupLine($"[yellow]Tokens:[/] prompt {jsonModeResponse.PromptTokens}, completion {jsonModeResponse.CompletionTokens}");
            AnsiConsole.MarkupLine("[cyan]JSON Mode response:[/]");
            AnsiConsole.WriteLine(jsonModeResponse.Content);
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(jsonModeResponse.ErrorMessage ?? "Unknown error")}");
        }

        AnsiConsole.WriteLine();

        // --- Structured Outputs ---
        AnsiConsole.MarkupLine("[bold underline]2. Structured Outputs[/]");

        const string schema = """
            {
                "type": "object",
                "properties": {
                    "colors": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "name": { "type": "string" },
                                "hex": { "type": "string" }
                            },
                            "required": ["name", "hex"],
                            "additionalProperties": false
                        }
                    }
                },
                "required": ["colors"],
                "additionalProperties": false
            }
            """;

        var schemaRequest = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "List 3 colors with their hex codes")],
            Model: model,
            Temperature: 0,
            MaxTokens: 500);

        var schemaOptions = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "color_list",
            SchemaDescription: "A list of colors with hex codes",
            JsonSchema: schema);

        var schemaResponse = await jsonFeature.GetChatCompletionWithJsonOutputAsync(
            schemaRequest, schemaOptions, cancellationToken);

        if (schemaResponse.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[green]Model:[/] {Markup.Escape(schemaResponse.Model)}");
            AnsiConsole.MarkupLine($"[yellow]Tokens:[/] prompt {schemaResponse.PromptTokens}, completion {schemaResponse.CompletionTokens}");

            if (schemaResponse.Refusal is not null)
            {
                AnsiConsole.MarkupLine($"[red]Refusal:[/] {Markup.Escape(schemaResponse.Refusal)}");
            }
            else
            {
                AnsiConsole.MarkupLine("[cyan]Structured Output response:[/]");
                AnsiConsole.WriteLine(schemaResponse.Content);
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(schemaResponse.ErrorMessage ?? "Unknown error")}");
        }
    }
}
