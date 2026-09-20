using System.Reflection;

namespace Cisharp.Console.Scenarios;

public static class ScenarioRegistry
{
    public static IReadOnlyList<IScenario> GetScenarios()
    {
        var scenarioType = typeof(IScenario);

        var scenarios = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && scenarioType.IsAssignableFrom(t))
            .Select(t => (IScenario)Activator.CreateInstance(t)!)
            .ToList();

        var duplicate = scenarios
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate scenario name detected: {duplicate.Key}");

        return scenarios
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
