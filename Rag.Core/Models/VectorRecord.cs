namespace Rag.Core.Models;

/// <summary>
/// Represents an embedded text chunk stored in the vector store.
/// </summary>
public sealed record VectorRecord(string Id, string SourceId, int ChunkIndex, string Text, float[] Vector);
