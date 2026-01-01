using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Chunking;

/// <summary>
/// Splits text into word-based chunks with configurable overlap.
/// </summary>
public class WordChunker : ITextChunker
{
    private readonly int _chunkWordCount;
    private readonly int _overlapWordCount;

    /// <summary>
    /// Creates a new word chunker.
    /// </summary>
    public WordChunker(int chunkWordCount = 180, int overlapWordCount = 40)
    {
        if (chunkWordCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkWordCount), "Chunk size must be positive.");
        }

        if (overlapWordCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapWordCount), "Overlap must be non-negative.");
        }

        _chunkWordCount = chunkWordCount;
        _overlapWordCount = System.Math.Min(overlapWordCount, chunkWordCount - 1);
    }

    /// <summary>
    /// Produces ordered text chunks with overlap between consecutive chunks.
    /// </summary>
    public IEnumerable<TextChunk> Chunk(string sourceId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        if (sourceId is null)
        {
            throw new ArgumentNullException(nameof(sourceId));
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            yield break;
        }

        var step = System.Math.Max(1, _chunkWordCount - _overlapWordCount);
        var chunkIndex = 0;

        for (var start = 0; start < words.Length; start += step)
        {
            var length = System.Math.Min(_chunkWordCount, words.Length - start);
            var chunkText = string.Join(" ", words.Skip(start).Take(length));

            yield return new TextChunk($"{sourceId}::chunk::{chunkIndex}", sourceId, chunkIndex, chunkText);
            chunkIndex++;
        }
    }
}
