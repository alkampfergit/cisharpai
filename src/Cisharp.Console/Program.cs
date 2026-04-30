using Cisharp.Console.Configuration;
using Cisharp.Console.Scenarios;
using Spectre.Console;

DotEnv.Load();

var scenarios = ScenarioRegistry.GetScenarios();
if (scenarios.Count == 0)
{
    AnsiConsole.MarkupLine("[red]No scenarios found.[/]");
    return;
}

var scenarioMap = scenarios.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
var menuItems = scenarioMap.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
menuItems.Add("Exit");

using var cts = new CancellationTokenSource();
global::System.Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

AnsiConsole.MarkupLine("[bold]Cisharp Console[/]");
AnsiConsole.MarkupLine("Select a scenario to run.");

while (true)
{
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Scenario")
            .PageSize(10)
            .AddChoices(menuItems));

    if (choice.Equals("Exit", StringComparison.OrdinalIgnoreCase))
        break;

    var scenario = scenarioMap[choice];

    AnsiConsole.Clear();
    AnsiConsole.MarkupLine($"[bold]{Markup.Escape(scenario.Name)}[/]");
    AnsiConsole.MarkupLine($"[grey]{Markup.Escape(scenario.Description)}[/]");
    AnsiConsole.WriteLine();

    try
    {
        await scenario.RunAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        AnsiConsole.MarkupLine("[yellow]Canceled.[/]");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
    }

    AnsiConsole.WriteLine();
    if (!AnsiConsole.Confirm("Run another scenario?"))
        break;

    AnsiConsole.Clear();
}
