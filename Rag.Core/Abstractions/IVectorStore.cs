using Rag.Core.Models;

namespace Rag.Core.Abstractions;

/// <summary>
/// Persists and queries vectorized text chunks.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Inserts or replaces vector records.
    /// </summary>
    Task UpsertAsync(IEnumerable<VectorRecord> records, CancellationToken ct = default);

    /// <summary>
    /// Executes a similarity search returning ranked chunks above a threshold.
    /// </summary>
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(float[] queryVector, int topK, double threshold, CancellationToken ct = default);
}
