using Azure.Core;

namespace Cisharpai.Tests.Azure;

public sealed class FakeTokenCredential : TokenCredential
{
    private readonly string _token;

    public FakeTokenCredential(string token)
    {
        _token = token;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new AccessToken(_token, DateTimeOffset.UtcNow.AddHours(1));
    }

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new AccessToken(_token, DateTimeOffset.UtcNow.AddHours(1)));
    }
}
