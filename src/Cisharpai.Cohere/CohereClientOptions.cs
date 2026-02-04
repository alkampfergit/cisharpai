namespace Cisharpai.Cohere;

public sealed class CohereClientOptions
{
    public string BaseUrl { get; set; } = "https://api.cohere.com/v2/";

    public string ApiKey { get; set; } = string.Empty;
}
