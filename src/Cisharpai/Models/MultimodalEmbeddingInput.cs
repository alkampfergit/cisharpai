namespace Cisharpai.Models;

public abstract record EmbeddingContentPart;

public sealed record TextEmbeddingContent(string Text) : EmbeddingContentPart;

public sealed record ImageEmbeddingContent(string ImagePath) : EmbeddingContentPart;

public sealed record MultimodalEmbeddingInput(
    IReadOnlyList<EmbeddingContentPart> Content);
