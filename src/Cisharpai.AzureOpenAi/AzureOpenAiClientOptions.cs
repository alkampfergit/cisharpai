namespace Cisharpai.AzureOpenAi;

public sealed class AzureOpenAiClientOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public string ApiVersion { get; set; } = "2024-02-01";
}
