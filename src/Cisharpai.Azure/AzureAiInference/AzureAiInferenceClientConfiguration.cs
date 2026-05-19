namespace Cisharpai.Azure.AzureAiInference;

public sealed record AzureAiInferenceClientConfiguration : CisharpaiClientConfiguration
{
    public override CisharpaiProvider Provider => CisharpaiProvider.AzureAiInference;

    public required string Endpoint { get; init; }

    public string ApiVersion { get; init; } = "2024-05-01-preview";

    public required string ModelId { get; init; }
}
