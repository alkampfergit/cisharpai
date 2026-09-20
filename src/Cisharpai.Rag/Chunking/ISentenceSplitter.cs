namespace Cisharpai.Rag.Chunking;

/// <summary>
/// Splits text into sentences. The default implementation targets English prose
/// and will mis-split on abbreviations like "Dr." and "e.g.". Inject your own
/// implementation for domain-specific or multilingual sentence segmentation.
/// </summary>
public interface ISentenceSplitter
{
    IReadOnlyList<string> Split(string text);
}
