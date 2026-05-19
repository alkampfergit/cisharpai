namespace Cisharpai.Cohere;

public sealed record CohereClientConfiguration : CisharpaiClientConfiguration
{
    public override CisharpaiProvider Provider => CisharpaiProvider.Cohere;

    public string BaseUrl { get; init; } = "https://api.cohere.com/v2/";

    public string? DefaultModel { get; init; }
}
