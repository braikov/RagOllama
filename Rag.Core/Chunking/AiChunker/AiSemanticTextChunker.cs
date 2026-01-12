using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Chunking.AiChunker;

/// <summary>
/// LLM-assisted chunker that groups paragraphs semantically and falls back to rule-based splitting on failure.
/// </summary>
public class AiSemanticTextChunker : ITextChunker
{
    private readonly IChunkPlanner _planner;
    private readonly AiChunkerOptions _options;
    private readonly ParagraphSplitter _paragraphSplitter = new();
    private readonly ChunkPlanValidator _validator = new();
    private readonly SentenceSplitter _sentenceSplitter = new();

    public AiSemanticTextChunker(IChunkPlanner planner, AiChunkerOptions? options = null)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _options = options ?? new AiChunkerOptions();
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

        var paragraphs = _paragraphSplitter.Split(text);
        if (paragraphs.Count == 0)
        {
            yield break;
        }

        IReadOnlyList<ChunkPlanItem> planChunks;

        if (paragraphs.Count > _options.MaxParagraphsPerRequest && _options.FallbackToRuleBasedOnError)
        {
            planChunks = BuildFallbackPlan(paragraphs);
        }
        else
        {
            try
            {
                var plan = _planner.PlanAsync(paragraphs, _options, CancellationToken.None).GetAwaiter().GetResult();
                if (!_validator.TryValidate(paragraphs.Count, plan, out var error))
                {
                    throw new InvalidOperationException($"Invalid chunk plan: {error}");
                }

                planChunks = plan.Chunks;
            }
            catch
            {
                if (!_options.FallbackToRuleBasedOnError)
                {
                    throw;
                }

                planChunks = BuildFallbackPlan(paragraphs);
            }
        }

        var overlap = string.Empty;
        var chunkIndex = 0;

        foreach (var planChunk in planChunks)
        {
            var orderedParagraphs = planChunk.Paragraphs.OrderBy(i => i).Select(i => paragraphs[i]).ToList();
            var bodyText = string.Join("\n\n", orderedParagraphs.Select(p => p.Text));

            var bodyWithOverlap = string.IsNullOrWhiteSpace(overlap)
                ? bodyText
                : $"{overlap}\n\n{bodyText}";

            var headerPath = orderedParagraphs.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.HeaderPath))?.HeaderPath ?? string.Empty;
            var finalText = BuildChunkText(bodyWithOverlap, headerPath);

            var chunkId = $"{sourceId}::chunk::{chunkIndex:D5}";
            yield return new TextChunk(chunkId, sourceId, chunkIndex, finalText);

            overlap = BuildOverlap(bodyText);
            chunkIndex++;
        }
    }

    private string BuildChunkText(string body, string headerPath)
    {
        if (!_options.IncludeHeaderPrefix || string.IsNullOrWhiteSpace(headerPath))
        {
            return body;
        }

        var prefix = _options.HeaderPrefixFormat.Replace("{path}", headerPath);
        return $"{prefix}{body}".Trim();
    }

    private string BuildOverlap(string body)
    {
        if (_options.OverlapSentences <= 0)
        {
            return string.Empty;
        }

        var sentences = _sentenceSplitter.Split(body);
        if (sentences.Count == 0)
        {
            return string.Empty;
        }

        var take = System.Math.Min(_options.OverlapSentences, sentences.Count);
        return string.Join(" ", sentences.Skip(sentences.Count - take));
    }

    private IReadOnlyList<ChunkPlanItem> BuildFallbackPlan(IReadOnlyList<Paragraph> paragraphs)
    {
        var chunks = new List<ChunkPlanItem>();
        var current = new List<int>();
        var words = 0;

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraphWords = CountWords(paragraphs[i].Text);

            if (words + paragraphWords > _options.MaxWords && current.Count > 0)
            {
                chunks.Add(new ChunkPlanItem(current.ToList()));
                current.Clear();
                words = 0;
            }

            current.Add(i);
            words += paragraphWords;

            if (words >= _options.TargetWords)
            {
                chunks.Add(new ChunkPlanItem(current.ToList()));
                current.Clear();
                words = 0;
            }
        }

        if (current.Count > 0)
        {
            if (chunks.Count > 0 && words < _options.MinWords)
            {
                var merged = chunks[^1].Paragraphs.Concat(current).ToList();
                chunks[^1] = new ChunkPlanItem(merged);
            }
            else
            {
                chunks.Add(new ChunkPlanItem(current.ToList()));
            }
        }

        return chunks;
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }
}
