using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Services;

/// <summary>
/// Retrieves the most relevant chunks for a query using embeddings and vector search.
/// </summary>
public class VectorRetriever
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;

    /// <summary>
    /// Creates a retriever with embedding provider and vector store.
    /// </summary>
    public VectorRetriever(IEmbeddingProvider embeddingProvider, IVectorStore vectorStore)
    {
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    /// <summary>
    /// Retrieves top chunks matching the query above the given threshold.
    /// </summary>
    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, double threshold = 0.72, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return Array.Empty<RetrievedChunk>();
        }

        var queryVector = await _embeddingProvider.EmbedAsync(query, ct).ConfigureAwait(false);
        var results = await _vectorStore.SearchAsync(queryVector, topK, threshold, ct).ConfigureAwait(false);

        return results;
    }
}
