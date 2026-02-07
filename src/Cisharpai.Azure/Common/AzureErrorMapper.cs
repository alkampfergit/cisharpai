namespace Cisharpai.Azure.Common;

/// <summary>
/// Utility class for mapping HTTP status codes to user-friendly error messages.
/// Provides consistent error messaging across Azure AI clients.
/// </summary>
internal static class AzureErrorMapper
{
    /// <summary>
    /// Maps an HTTP status code to a descriptive error message.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="rawMessage">Optional raw error message from the response.</param>
    /// <returns>A user-friendly error message.</returns>
    public static string MapHttpStatusToMessage(int statusCode, string? rawMessage = null)
    {
        var baseMessage = statusCode switch
        {
            401 => "Authentication failed",
            403 => "Access denied",
            404 => "Model or endpoint not found",
            422 => "Invalid parameter or unsupported by model",
            429 => "Rate limit exceeded",
            >= 500 => "Server error",
            _ => $"Request failed (HTTP {statusCode})"
        };

        return string.IsNullOrWhiteSpace(rawMessage)
            ? baseMessage
            : $"{baseMessage}: {rawMessage}";
    }
}
