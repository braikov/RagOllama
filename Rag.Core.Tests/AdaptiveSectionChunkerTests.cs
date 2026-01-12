using System.Text;
using Rag.Core.Chunking.Adaptive;
using Xunit;

namespace Rag.Core.Tests;

public class AdaptiveSectionChunkerTests
{
    [Fact]
    public void Includes_header_path_for_markdown()
    {
        var options = new AdaptiveSectionChunkerOptions
        {
            TargetWords = 20,
            MaxWords = 40,
            MinWords = 5,
            OverlapSentences = 0,
            OverlapRatio = 0
        };

        var chunker = new AdaptiveSectionChunker(options);

        var text = """
        # Title
        ## Subsection
        This is a small paragraph that should remain within a single chunk for testing the header prefix.
        """;

        var chunks = chunker.Chunk("doc", text).ToList();

        Assert.Single(chunks);
        Assert.StartsWith("Section: Title > Subsection", chunks[0].Text);
        Assert.Equal(0, chunks[0].ChunkIndex);
    }

    [Fact]
    public void Splits_plain_text_headings_into_sections()
    {
        var options = new AdaptiveSectionChunkerOptions
        {
            TargetWords = 30,
            MaxWords = 60,
            MinWords = 10,
            OverlapSentences = 0,
            OverlapRatio = 0
        };

        var chunker = new AdaptiveSectionChunker(options);

        var text = """
        PRODUCTS:
        List of items that belong to the products section providing details for each item in brief sentences.

        PRICING:
        The pricing section explains how much each product costs and what discounts may apply.
        """;

        var chunks = chunker.Chunk("doc", text).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.StartsWith("Section: PRODUCTS", chunks[0].Text);
        Assert.StartsWith("Section: PRICING", chunks[1].Text);
    }

    [Fact]
    public void Respects_chunk_size_and_merges_small_tail()
    {
        var options = new AdaptiveSectionChunkerOptions
        {
            TargetWords = 30,
            MaxWords = 40,
            MinWords = 15,
            OverlapSentences = 0,
            OverlapRatio = 0,
            IncludeHeaderPathPrefix = false
        };

        var chunker = new AdaptiveSectionChunker(options);

        var baseParagraph = "Sentence one for the paragraph. Sentence two keeps adding words. Sentence three continues the flow.";
        var textBuilder = new StringBuilder();
        for (var i = 0; i < 5; i++)
        {
            textBuilder.AppendLine(baseParagraph);
            textBuilder.AppendLine();
        }

        var chunks = chunker.Chunk("doc", textBuilder.ToString()).ToList();

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(WordCount(c.Text) <= options.MaxWords));

        if (chunks.Count > 1)
        {
            Assert.True(WordCount(chunks[^1].Text) >= options.MinWords);
        }
    }

    [Fact]
    public void Adds_overlap_from_previous_chunk()
    {
        var options = new AdaptiveSectionChunkerOptions
        {
            TargetWords = 12,
            MaxWords = 40,
            MinWords = 6,
            OverlapSentences = 1,
            OverlapRatio = 0,
            HeaderPrefixFormat = "Section: {path}\n\n"
        };

        var chunker = new AdaptiveSectionChunker(options);

        var text = """
        # Guide
        First sentence stays here. Second sentence is also here. Third sentence ensures we cross the target words.

        Another paragraph starts a new chunk for overlap validation.
        """;

        var chunks = chunker.Chunk("doc", text).ToList();

        Assert.True(chunks.Count >= 2);

        var firstBody = RemovePrefix(chunks[0].Text);
        var lastSentence = "Third sentence ensures we cross the target words.";
        Assert.Contains(lastSentence, firstBody);

        var secondBody = RemovePrefix(chunks[1].Text);
        Assert.StartsWith(lastSentence, secondBody);
    }

    private static int WordCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static string RemovePrefix(string text)
    {
        if (!text.StartsWith("Section:", StringComparison.Ordinal))
        {
            return text;
        }

        var separator = "\n\n";
        var index = text.IndexOf(separator, StringComparison.Ordinal);
        if (index < 0)
        {
            return text;
        }

        return text[(index + separator.Length)..];
    }
}
