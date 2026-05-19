namespace Cisharpai.Azure.AzureOpenAi;

public sealed record AzureOpenAiClientConfiguration : CisharpaiClientConfiguration
{
    public override CisharpaiProvider Provider => CisharpaiProvider.AzureOpenAi;

    public required string Endpoint { get; init; }

    public required string DeploymentName { get; init; }

    public string ApiVersion { get; init; } = "2024-10-21";

    public string? DefaultModel { get; init; }

    public string? ModelName { get; init; }

    public string? ReasoningEffort { get; init; }

    public string? TextVerbosity { get; init; }
}
