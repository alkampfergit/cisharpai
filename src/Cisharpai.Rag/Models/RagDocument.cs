namespace Cisharpai.Rag.Models;

/// <summary>A source document. The caller owns identifier uniqueness; text may be empty.</summary>
public sealed record RagDocument(string Id, string Text);
