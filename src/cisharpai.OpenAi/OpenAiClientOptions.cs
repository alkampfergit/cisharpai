namespace cisharpai.OpenAi;

public sealed class OpenAiClientOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    public string ApiKey { get; set; } = string.Empty;

    public string? Organization { get; set; }
}
