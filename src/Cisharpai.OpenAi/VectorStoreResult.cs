namespace Cisharpai.OpenAi;

/// <summary>
/// Result of a vector store operation. Follows the library convention:
/// <see cref="IsSuccess"/> is <c>false</c> on API errors, with <see cref="ErrorMessage"/>
/// describing the failure — no exceptions for API errors.
/// </summary>
public sealed record VectorStoreResult<T>(
    bool IsSuccess,
    T? Value,
    string? ErrorMessage = null)
{
    /// <summary>Raw JSON response body from the API, when available.</summary>
    public string? RawResponseJson { get; init; }

    /// <summary>Raw JSON request body sent to the API, when available.</summary>
    public string? RawRequestJson { get; init; }

    public static VectorStoreResult<T> Success(T value, string? rawResponseJson = null, string? rawRequestJson = null)
        => new(true, value) { RawResponseJson = rawResponseJson, RawRequestJson = rawRequestJson };

    public static VectorStoreResult<T> Error(string errorMessage, string? rawResponseJson = null, string? rawRequestJson = null)
        => new(false, default, errorMessage) { RawResponseJson = rawResponseJson, RawRequestJson = rawRequestJson };
}
