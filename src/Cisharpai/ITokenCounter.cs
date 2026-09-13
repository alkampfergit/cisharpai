namespace Cisharpai;

/// <summary>
/// Counts the number of tokens in a string for a specific model's tokenizer.
/// Each instance is constructed for one model — the model is baked in, not passed per call.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Error contract — deliberate departure from <c>IsSuccess</c>.</strong>
/// Implementations backed by a remote API (e.g. <c>CohereTokenCounter</c>) throw
/// <see cref="LlmHttpRequestException"/> on provider failure instead of
/// returning a result envelope. This is intentional: a token count has no meaningful
/// degraded value. The <c>IsSuccess</c> / <c>ErrorMessage</c> pattern on chat and
/// embedding clients exists so a caller can inspect a partial or failed response and
/// carry on; a count that is silently <c>0</c> or <c>-1</c> would corrupt a context
/// budget, which is precisely the class of failure the rest of this library eliminates.
/// Failure here is exceptional, not ordinary control flow.
/// </para>
/// <para>
/// Local implementations (e.g. <c>TiktokenCounter</c>) complete synchronously and do
/// not throw on provider failure because there is no provider.
/// </para>
/// </remarks>
public interface ITokenCounter
{
    ValueTask<int> CountAsync(string text, CancellationToken cancellationToken = default);
}
