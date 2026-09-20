using Microsoft.ML.Tokenizers;

namespace Cisharpai.Rag.Tokenization;

/// <summary>
/// Local, synchronous token counter backed by <c>Microsoft.ML.Tokenizers</c>.
/// The tokenizer instance is cached and reused; concurrent <see cref="CountAsync"/> calls are safe.
/// </summary>
public sealed class TiktokenCounter : ITokenCounter
{
    private readonly Tokenizer _tokenizer;

    /// <param name="modelName">
    /// A model name recognized by <see cref="TiktokenTokenizer.CreateForModel"/>
    /// (e.g. <c>"gpt-4o"</c>, <c>"gpt-4"</c>, <c>"gpt-3.5-turbo"</c>).
    /// </param>
    public TiktokenCounter(string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        _tokenizer = TiktokenTokenizer.CreateForModel(modelName);
    }

    /// <summary>
    /// Counts the tokens in <paramref name="text"/> using the local tokenizer.
    /// This is a synchronous operation that completes without allocation via <see cref="ValueTask{T}"/>.
    /// </summary>
    public ValueTask<int> CountAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<int>(_tokenizer.CountTokens(text));
    }

    /// <summary>
    /// Synchronously counts the tokens in <paramref name="text"/>.
    /// Prefer this over <see cref="CountAsync"/> when the caller is already synchronous.
    /// </summary>
    public int CountTokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _tokenizer.CountTokens(text);
    }

    /// <summary>
    /// Returns the UTF-16 index immediately following the last character that fits within
    /// <paramref name="maxTokenCount"/> tokens from the start. O(n) single-pass — no counting
    /// loop. When no tokens fit the result is 0; when all tokens fit the result is
    /// <c>text.Length</c>.
    /// </summary>
    public int GetIndexByTokenCount(string text, int maxTokenCount)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(maxTokenCount);
        if (maxTokenCount == 0) return 0;
        return _tokenizer.GetIndexByTokenCount(text, maxTokenCount, out _, out _);
    }

    /// <summary>
    /// Returns the UTF-16 index where the last <paramref name="maxTokenCount"/> tokens begin
    /// (counting from the end). When the entire text fits, the result is 0; when no tokens fit
    /// the result is <c>text.Length</c>.
    /// </summary>
    public int GetIndexByTokenCountFromEnd(string text, int maxTokenCount)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(maxTokenCount);
        if (maxTokenCount == 0) return text.Length;
        return _tokenizer.GetIndexByTokenCountFromEnd(text, maxTokenCount, out _, out _);
    }
}
