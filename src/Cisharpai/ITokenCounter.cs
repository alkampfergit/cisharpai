namespace Cisharpai;

/// <summary>
/// Counts the number of tokens in a string for a specific model's tokenizer.
/// Each instance is constructed for one model — the model is baked in, not passed per call.
/// </summary>
public interface ITokenCounter
{
    ValueTask<int> CountAsync(string text, CancellationToken cancellationToken = default);
}
