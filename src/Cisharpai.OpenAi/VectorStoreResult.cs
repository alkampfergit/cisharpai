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
    public static VectorStoreResult<T> Success(T value) => new(true, value);
    public static VectorStoreResult<T> Error(string errorMessage) => new(false, default, errorMessage);
}
