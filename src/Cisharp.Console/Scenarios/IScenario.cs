namespace Cisharp.Console.Scenarios;

public interface IScenario
{
    string Name { get; }
    string Description { get; }
    Task RunAsync(CancellationToken cancellationToken = default);
}
