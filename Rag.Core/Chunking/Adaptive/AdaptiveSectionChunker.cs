using System.Text;
using System.Text.RegularExpressions;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Chunking.Adaptive;

/// <summary>
/// Adaptive chunker that groups text by sections/headings and accumulates paragraphs toward target sizes.
/// </summary>
public class AdaptiveSectionChunker : ITextChunker
{
    private static readonly Regex WhitespaceReducer = new(@"\s+", RegexOptions.Compiled);

    private readonly AdaptiveSectionChunkerOptions _options;
    private readonly SectionParser _sectionParser;
    private readonly SentenceSplitter _sentenceSplitter;

    public AdaptiveSectionChunker(AdaptiveSectionChunkerOptions? options = null)
    {
        _options = Validate(options ?? new AdaptiveSectionChunkerOptions());
        _sectionParser = new SectionParser(_options.TrimWhitespace);
        _sentenceSplitter = new SentenceSplitter();
    }

    public IEnumerable<TextChunk> Chunk(string sourceId, string text)
    {
        if (sourceId is null)
        {
            throw new ArgumentNullException(nameof(sourceId));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var chunkIndex = 0;

        foreach (var section in _sectionParser.Parse(text))
        {
            var paragraphs = GetParagraphs(section.Content);
            if (paragraphs.Count == 0)
            {
                continue;
            }

            var bodies = BuildChunkBodies(paragraphs);

            if (bodies.Count > 1 && CountWords(bodies[^1]) < _options.MinWords)
            {
                bodies[^2] = MergeBodies(bodies[^2], bodies[^1]);
                bodies.RemoveAt(bodies.Count - 1);
            }

            string? overlap = null;

            foreach (var body in bodies)
            {
                var bodyWithOverlap = CombineWithOverlap(body, overlap);

                var finalText = BuildChunkText(section.Path, bodyWithOverlap);
                finalText = Truncate(finalText);

                var chunkId = $"{sourceId}::chunk::{chunkIndex:D5}";

                yield return new TextChunk(chunkId, sourceId, chunkIndex, finalText);

                overlap = BuildOverlap(body);
                chunkIndex++;
            }
        }
    }

    private static AdaptiveSectionChunkerOptions Validate(AdaptiveSectionChunkerOptions options)
    {
        if (options.TargetWords <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TargetWords), "TargetWords must be positive.");
        }

        if (options.MaxWords < options.TargetWords)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxWords), "MaxWords must be greater than or equal to TargetWords.");
        }

        if (options.MinWords < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MinWords), "MinWords must be non-negative.");
        }

        if (options.OverlapRatio < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.OverlapRatio), "OverlapRatio must be non-negative.");
        }

        if (options.OverlapSentences < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.OverlapSentences), "OverlapSentences must be non-negative.");
        }

        if (options.MaxHeaderPrefixChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxHeaderPrefixChars), "MaxHeaderPrefixChars must be positive.");
        }

        if (options.MaxChunkTextCharsForEmbedding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxChunkTextCharsForEmbedding), "MaxChunkTextCharsForEmbedding must be non-negative.");
        }

        return options;
    }

    private List<string> GetParagraphs(string content)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        var normalized = content.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var buffer = new List<string>();

        foreach (var line in lines)
        {
            var working = _options.TrimWhitespace ? line.Trim() : line;

            if (_options.NormalizeWhitespace)
            {
                working = WhitespaceReducer.Replace(working, " ");
            }

            if (string.IsNullOrWhiteSpace(working))
            {
                if (buffer.Count > 0)
                {
                    result.Add(string.Join(" ", buffer));
                    buffer.Clear();
                }

                continue;
            }

            buffer.Add(working);
        }

        if (buffer.Count > 0)
        {
            result.Add(string.Join(" ", buffer));
        }

        return result;
    }

    private List<string> BuildChunkBodies(List<string> paragraphs)
    {
        var bodies = new List<string>();
        var index = 0;

        while (index < paragraphs.Count)
        {
            var parts = new List<string>();
            var wordCount = 0;

            while (index < paragraphs.Count)
            {
                var paragraph = paragraphs[index];
                var paragraphWords = CountWords(paragraph);

                if (paragraphWords > _options.MaxWords)
                {
                    var splits = SplitParagraph(paragraph).ToList();
                    paragraphs.RemoveAt(index);
                    paragraphs.InsertRange(index, splits);
                    continue;
                }

                if (wordCount >= _options.TargetWords && wordCount + paragraphWords > _options.MaxWords)
                {
                    break;
                }

                if (wordCount + paragraphWords > _options.MaxWords)
                {
                    var splits = SplitParagraph(paragraph).ToList();
                    paragraphs.RemoveAt(index);
                    paragraphs.InsertRange(index, splits);
                    continue;
                }

                parts.Add(paragraph);
                wordCount += paragraphWords;
                index++;

                if (wordCount >= _options.TargetWords)
                {
                    break;
                }
            }

            if (parts.Count > 0)
            {
                bodies.Add(string.Join("\n\n", parts));
            }
            else
            {
                index++;
            }
        }

        return bodies;
    }

    private IEnumerable<string> SplitParagraph(string paragraph)
    {
        var sentences = _sentenceSplitter.Split(paragraph).ToList();
        if (sentences.Count == 0)
        {
            foreach (var chunk in SplitByWords(paragraph))
            {
                yield return chunk;
            }

            yield break;
        }

        var buffer = new List<string>();
        var wordCount = 0;

        foreach (var sentence in sentences)
        {
            var words = CountWords(sentence);
            if (words > _options.MaxWords)
            {
                if (buffer.Count > 0)
                {
                    yield return string.Join(" ", buffer);
                    buffer.Clear();
                    wordCount = 0;
                }

                foreach (var chunk in SplitByWords(sentence))
                {
                    yield return chunk;
                }

                continue;
            }

            if (wordCount + words > _options.MaxWords && buffer.Count > 0)
            {
                yield return string.Join(" ", buffer);
                buffer.Clear();
                wordCount = 0;
            }

            buffer.Add(sentence);
            wordCount += words;
        }

        if (buffer.Count > 0)
        {
            yield return string.Join(" ", buffer);
        }
    }

    private IEnumerable<string> SplitByWords(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            yield break;
        }

        for (var start = 0; start < words.Length; start += _options.MaxWords)
        {
            var length = System.Math.Min(_options.MaxWords, words.Length - start);
            yield return string.Join(" ", words.Skip(start).Take(length));
        }
    }

    private string BuildChunkText(IReadOnlyList<string> sectionPath, string body)
    {
        var parts = new List<string>();

        if (_options.IncludeHeaderPathPrefix && sectionPath.Count > 0)
        {
            var path = string.Join(" > ", sectionPath);
            var prefix = _options.HeaderPrefixFormat.Replace("{path}", path);

            if (prefix.Length > _options.MaxHeaderPrefixChars)
            {
                prefix = prefix[.._options.MaxHeaderPrefixChars];
            }

            parts.Add(prefix.TrimEnd());
        }

        parts.Add(body.Trim());

        return string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private string CombineWithOverlap(string body, string? overlap)
    {
        if (string.IsNullOrWhiteSpace(overlap))
        {
            return body;
        }

        var bodyWords = CountWords(body);
        var overlapWords = overlap.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (bodyWords + overlapWords.Length <= _options.MaxWords)
        {
            return $"{overlap}\n\n{body}";
        }

        var allowedOverlap = System.Math.Max(0, _options.MaxWords - bodyWords);
        if (allowedOverlap <= 0)
        {
            return body;
        }

        var truncatedOverlap = string.Join(" ", overlapWords.TakeLast(allowedOverlap));
        return $"{truncatedOverlap}\n\n{body}";
    }

    private string? BuildOverlap(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        if (_options.OverlapSentences > 0)
        {
            var sentences = _sentenceSplitter.Split(body).ToList();
            if (sentences.Count == 0)
            {
                return null;
            }

            var take = System.Math.Min(_options.OverlapSentences, sentences.Count);
            var overlapSentences = sentences.Skip(System.Math.Max(0, sentences.Count - take));
            var overlapText = string.Join(" ", overlapSentences).Trim();
            return overlapText.Length > 0 ? overlapText : null;
        }

        if (_options.OverlapRatio > 0)
        {
            var words = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length == 0)
            {
                return null;
            }

            var take = System.Math.Max(1, (int)System.Math.Ceiling(words.Length * System.Math.Min(_options.OverlapRatio, 1)));
            take = System.Math.Min(take, words.Length);

            var overlapWords = words.Skip(words.Length - take);
            var overlapText = string.Join(" ", overlapWords);
            return overlapText.Length > 0 ? overlapText : null;
        }

        return null;
    }

    private static string MergeBodies(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return $"{first}\n\n{second}";
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length;
    }

    private string Truncate(string text)
    {
        if (_options.MaxChunkTextCharsForEmbedding == 0 || text.Length <= _options.MaxChunkTextCharsForEmbedding)
        {
            return text;
        }

        return text[.._options.MaxChunkTextCharsForEmbedding];
    }
}
