namespace Cisharpai.OpenAi;

public sealed record OpenAiClientConfiguration : CisharpaiClientConfiguration
{
    public override CisharpaiProvider Provider => CisharpaiProvider.OpenAi;

    public string BaseUrl { get; init; } = "https://api.openai.com/v1/";

    public string? DefaultModel { get; init; }

    public string? Organization { get; init; }

    public string? ReasoningEffort { get; init; }

    public string? TextVerbosity { get; init; }
}
