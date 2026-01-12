namespace Rag.Core.Chunking.Adaptive;

/// <summary>
/// Configuration options for adaptive section-based chunking.
/// </summary>
public sealed class AdaptiveSectionChunkerOptions
{
    public int TargetWords { get; init; } = 700;

    public int MaxWords { get; init; } = 1100;

    public int MinWords { get; init; } = 200;

    public double OverlapRatio { get; init; } = 0.15;

    public int OverlapSentences { get; init; } = 2;

    public int MaxHeaderPrefixChars { get; init; } = 300;

    public int MaxChunkTextCharsForEmbedding { get; init; } = 0;

    public bool IncludeHeaderPathPrefix { get; init; } = true;

    public string HeaderPrefixFormat { get; init; } = "Section: {path}\n\n";

    public bool TrimWhitespace { get; init; } = true;

    public bool NormalizeWhitespace { get; init; } = true;
}
