namespace Rag.Core.Models;

/// <summary>
/// Represents a chunk of text produced during chunking.
/// </summary>
public sealed record TextChunk(string Id, string SourceId, int ChunkIndex, string Text);
