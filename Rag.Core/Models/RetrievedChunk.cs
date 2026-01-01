namespace Rag.Core.Models;

/// <summary>
/// Represents a chunk returned from similarity search with its score.
/// </summary>
public sealed record RetrievedChunk(string Id, string SourceId, int ChunkIndex, string Text, double Score);
