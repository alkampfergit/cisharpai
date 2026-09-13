namespace Cisharpai.Rag.Packing;

/// <summary>
/// Controls how the packer handles a chunk that does not fit within the remaining token budget.
/// </summary>
public enum OverflowStrategy
{
    /// <summary>Skip the oversized chunk and continue packing lower-ranked chunks.</summary>
    SkipAndContinue,

    /// <summary>Stop packing at the first chunk that does not fit.</summary>
    StopAtFirstMisfit
}
