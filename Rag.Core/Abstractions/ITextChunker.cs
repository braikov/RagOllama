using Rag.Core.Models;

namespace Rag.Core.Abstractions;

/// <summary>
/// Splits raw text into ordered chunks for downstream embedding and storage.
/// </summary>
public interface ITextChunker
{
    /// <summary>
    /// Splits the provided text into chunks tagged with source information.
    /// </summary>
    IEnumerable<TextChunk> Chunk(string sourceId, string text);
}
