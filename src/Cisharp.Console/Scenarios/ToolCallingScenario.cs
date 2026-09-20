using System.Text.Json;
using Cisharp.Console.Configuration;
using Cisharpai;
using Cisharpai.Anthropic;
using Cisharpai.Cohere;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Cisharp.Console.Scenarios;

public sealed class ToolCallingScenario : IScenario
{
    public string Name => "Tool calling";
    public string Description => "Demonstrates tool calling (function calling) with a weather tool across providers.";

    private static readonly JsonElement WeatherParameters = JsonDocument.Parse(
        """
        {
            "type": "object",
            "properties": {
                "location": {
                    "type": "string",
                    "description": "The city and country, e.g. Paris, France"
                },
                "unit": {
                    "type": "string",
                    "enum": ["celsius", "fahrenheit"],
                    "description": "The temperature unit"
                }
            },
            "required": ["location"],
            "additionalProperties": false
        }
        """).RootElement.Clone();

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        DotEnv.Load();

        var provider = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Which provider?")
                .AddChoices("OpenAI", "Anthropic", "Cohere"));

        var (client, model) = CreateClient(provider);
        var toolFeature = client.Features.Get<IToolCallingFeature>();

        if (toolFeature is null)
        {
            AnsiConsole.MarkupLine("[red]This provider does not support tool calling.[/]");
            return;
        }

        model = AnsiConsole.Ask("Model?", model);
        var prompt = AnsiConsole.Ask("User prompt?", "What is the current weather in Paris and Tokyo?");

        var tools = new ToolCallingOptions(
            Tools:
            [
                new ToolDefinition(
                    "get_current_weather",
                    "Get the current weather for a given location",
                    WeatherParameters)
            ]);

        // Step 1: Initial request
        AnsiConsole.MarkupLine("[blue]Step 1:[/] Sending request with tool definitions...");

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, prompt)],
            Model: model,
            Temperature: 0,
            MaxTokens: 1024);

        var response = await toolFeature.GetChatCompletionWithToolsAsync(request, tools, cancellationToken);

        if (!response.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(response.ErrorMessage ?? "Unknown error")}");
            return;
        }

        if (response.ToolCalls is null || response.ToolCalls.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Model did not request any tool calls.[/]");
            AnsiConsole.MarkupLine($"[bold]Response:[/] {Markup.Escape(response.Content)}");
            return;
        }

        // Display tool calls
        AnsiConsole.MarkupLine($"[green]Model requested {response.ToolCalls.Count} tool call(s):[/]");
        foreach (var toolCall in response.ToolCalls)
        {
            AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(toolCall.FunctionName)}[/] (id: {Markup.Escape(toolCall.Id)})");
            AnsiConsole.MarkupLine($"    Arguments: {Markup.Escape(toolCall.Arguments.GetRawText())}");
        }

        // Step 2: Execute tools with mock data and send results back
        AnsiConsole.MarkupLine("[blue]Step 2:[/] Executing tools with mock data and sending results back...");

        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, prompt),
            new(LlmRole.Assistant, "", ToolCalls: response.ToolCalls.ToList())
        };

        foreach (var toolCall in response.ToolCalls)
        {
            var result = GetMockWeather(toolCall.Arguments);
            AnsiConsole.MarkupLine($"  [dim]Mock result for {Markup.Escape(toolCall.FunctionName)}:[/] {Markup.Escape(result)}");
            messages.Add(new LlmMessage(LlmRole.Tool, result, ToolCallId: toolCall.Id));
        }

        var followUpRequest = new ChatCompletionRequest(
            Messages: messages,
            Model: model,
            Temperature: 0,
            MaxTokens: 1024);

        var finalResponse = await toolFeature.GetChatCompletionWithToolsAsync(followUpRequest, tools, cancellationToken);

        if (!finalResponse.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(finalResponse.ErrorMessage ?? "Unknown error")}");
            return;
        }

        // Display final response
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Final response:[/]");
        AnsiConsole.WriteLine(finalResponse.Content);
        AnsiConsole.MarkupLine($"[yellow]Tokens:[/] prompt {finalResponse.ChatCompletion.PromptTokens}, completion {finalResponse.ChatCompletion.CompletionTokens}");
    }

    private static (IChatCompletionClient client, string defaultModel) CreateClient(string provider)
    {
        var services = ScenarioHelpers.CreateServiceCollection();

        switch (provider)
        {
            case "OpenAI":
            {
                var apiKey = ScenarioHelpers.RequireEnv(DotEnv.OpenAiApiKey);
                services.AddOpenAiClient(options => { options.ApiKey = apiKey; });
                var sp = services.BuildServiceProvider();
                return (sp.GetRequiredService<IChatCompletionClient>(), "gpt-4.1-nano");
            }
            case "Anthropic":
            {
                var apiKey = ScenarioHelpers.RequireEnv(DotEnv.AnthropicApiKey);
                services.AddAnthropicClient(options => { options.ApiKey = apiKey; });
                var sp = services.BuildServiceProvider();
                return (sp.GetRequiredService<IChatCompletionClient>(), "claude-haiku-4-5-20251001");
            }
            case "Cohere":
            {
                var apiKey = ScenarioHelpers.RequireEnv(DotEnv.CohereApiKey);
                services.AddCohereChatClient(options => { options.ApiKey = apiKey; });
                var sp = services.BuildServiceProvider();
                return (sp.GetRequiredService<IChatCompletionClient>(), "command-a-03-2025");
            }
            default:
                throw new InvalidOperationException($"Unknown provider: {provider}");
        }
    }

    private static string GetMockWeather(JsonElement arguments)
    {
        var location = arguments.TryGetProperty("location", out var loc) ? loc.GetString() ?? "Unknown" : "Unknown";
        var unit = arguments.TryGetProperty("unit", out var u) ? u.GetString() ?? "celsius" : "celsius";

        // Return mock weather data based on location
        var (temp, condition) = location.ToLowerInvariant() switch
        {
            var l when l.Contains("paris") => (unit == "fahrenheit" ? "72" : "22", "Sunny with light clouds"),
            var l when l.Contains("tokyo") => (unit == "fahrenheit" ? "79" : "26", "Humid and partly cloudy"),
            var l when l.Contains("london") => (unit == "fahrenheit" ? "59" : "15", "Overcast with light rain"),
            var l when l.Contains("new york") => (unit == "fahrenheit" ? "68" : "20", "Clear skies"),
            _ => (unit == "fahrenheit" ? "70" : "21", "Partly cloudy")
        };

        var unitSymbol = unit == "fahrenheit" ? "F" : "C";
        return $"{{\"location\": \"{location}\", \"temperature\": \"{temp} {unitSymbol}\", \"condition\": \"{condition}\"}}";
    }
}
