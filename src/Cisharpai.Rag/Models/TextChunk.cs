namespace Cisharpai.Rag.Models;

/// <summary>An exact source slice, identified by document ID and zero-based chunk index.</summary>
/// <param name="DocumentId">The source document identifier.</param>
/// <param name="Index">Zero-based chunk index within the document.</param>
/// <param name="StartOffset">Zero-based UTF-16 offset into the source text, usable with Substring.</param>
/// <param name="Text">The unmodified source slice.</param>
public sealed record TextChunk(string DocumentId, int Index, int StartOffset, string Text);
