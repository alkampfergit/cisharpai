using Microsoft.Extensions.DependencyInjection;

namespace Cisharp.Console.Scenarios;

internal static class ScenarioHelpers
{
    public static ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    public static string RequireEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Environment variable {name} is required.");
        return value;
    }

    public static string? GetEnv(string name)
        => Environment.GetEnvironmentVariable(name);
}
