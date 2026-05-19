namespace Cisharpai;

public abstract record CisharpaiClientConfiguration
{
    public abstract CisharpaiProvider Provider { get; }

    public required string ApiKey { get; init; }
}
