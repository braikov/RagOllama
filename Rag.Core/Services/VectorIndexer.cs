using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Services;

/// <summary>
/// Coordinates chunking and embedding to index text into a vector store.
/// </summary>
public class VectorIndexer
{
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;

    /// <summary>
    /// Creates a new indexer with the required dependencies.
    /// </summary>
    public VectorIndexer(ITextChunker chunker, IEmbeddingProvider embeddingProvider, IVectorStore vectorStore)
    {
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    /// <summary>
    /// Chunks, embeds, and upserts the provided text.
    /// </summary>
    public async Task IndexTextAsync(string sourceId, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source id is required.", nameof(sourceId));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var records = new List<VectorRecord>();

        foreach (var chunk in _chunker.Chunk(sourceId, text))
        {
            ct.ThrowIfCancellationRequested();

            var vector = await _embeddingProvider.EmbedAsync(chunk.Text, ct).ConfigureAwait(false);
            records.Add(new VectorRecord(chunk.Id, chunk.SourceId, chunk.ChunkIndex, chunk.Text, vector));
        }

        if (records.Count > 0)
        {
            await _vectorStore.UpsertAsync(records, ct).ConfigureAwait(false);
        }
    }
}
