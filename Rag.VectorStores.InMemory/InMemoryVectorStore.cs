using Rag.Core.Abstractions;
using Rag.Core.Math;
using Rag.Core.Models;

namespace Rag.VectorStores.InMemory;

/// <summary>
/// Thread-safe in-memory vector store with cosine similarity ranking.
/// </summary>
public class InMemoryVectorStore : IVectorStore
{
    private readonly List<VectorRecord> _records = new();
    private readonly object _sync = new();

    /// <summary>
    /// Inserts or replaces vector records by identifier.
    /// </summary>
    public Task UpsertAsync(IEnumerable<VectorRecord> records, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        lock (_sync)
        {
            foreach (var record in records)
            {
                ct.ThrowIfCancellationRequested();

                var existingIndex = _records.FindIndex(r => r.Id == record.Id);
                if (existingIndex >= 0)
                {
                    _records[existingIndex] = record;
                }
                else
                {
                    _records.Add(record);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns TopK records above the similarity threshold using cosine similarity.
    /// </summary>
    public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(float[] queryVector, int topK, double threshold, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queryVector);

        if (topK <= 0)
        {
            return Task.FromResult<IReadOnlyList<RetrievedChunk>>(Array.Empty<RetrievedChunk>());
        }

        List<VectorRecord> snapshot;
        lock (_sync)
        {
            snapshot = _records.ToList();
        }

        var results = snapshot
            .Select(record =>
            {
                ct.ThrowIfCancellationRequested();
                var score = VectorMath.CosineSimilarity(queryVector, record.Vector);
                return new RetrievedChunk(record.Id, record.SourceId, record.ChunkIndex, record.Text, score);
            })
            .Where(r => r.Score >= threshold)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult<IReadOnlyList<RetrievedChunk>>(results);
    }
}
