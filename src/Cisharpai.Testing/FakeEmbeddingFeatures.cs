namespace Cisharpai.Testing;

/// <summary>
/// Controls which optional features are registered on a <see cref="FakeEmbeddingClient"/>.
/// </summary>
[Flags]
public enum FakeEmbeddingFeatures
{
    None = 0,
    ImageEmbedding = 1 << 0,
    MultimodalEmbedding = 1 << 1,
    All = ImageEmbedding | MultimodalEmbedding
}
