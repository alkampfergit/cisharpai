namespace Cisharpai;

public sealed record CisharpaiClientFactoryResult<T> where T : class
{
    public bool IsSuccess { get; private init; }

    public T? Client { get; private init; }

    public string? ErrorMessage { get; private init; }

    private CisharpaiClientFactoryResult() { }

    public static CisharpaiClientFactoryResult<T> Success(T client) =>
        new() { IsSuccess = true, Client = client };

    public static CisharpaiClientFactoryResult<T> Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
