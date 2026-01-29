using System.Net;

namespace Cisharpai;

public sealed class LlmHttpRequestException : HttpRequestException
{
    public string? ResponseBody { get; }

    public LlmHttpRequestException(
        HttpStatusCode statusCode,
        string? responseBody)
        : base(BuildMessage(statusCode, responseBody), inner: null, statusCode)
    {
        ResponseBody = responseBody;
    }

    private static string BuildMessage(HttpStatusCode statusCode, string? responseBody)
    {
        var message = $"HTTP request failed with status code {(int)statusCode} ({statusCode}).";

        if (!string.IsNullOrWhiteSpace(responseBody))
            message += $" Response body: {responseBody}";

        return message;
    }
}
