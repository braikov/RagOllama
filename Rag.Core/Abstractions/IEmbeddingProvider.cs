namespace Rag.Core.Abstractions;

/// <summary>
/// Generates vector embeddings for input text.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Computes an embedding for the provided text.
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
