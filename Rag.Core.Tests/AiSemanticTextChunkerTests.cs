using Rag.Core.Chunking.AiChunker;
using Rag.Core.Models;
using Xunit;

namespace Rag.Core.Tests;

public class AiSemanticTextChunkerTests
{
    [Fact]
    public void Uses_plan_when_valid()
    {
        var paragraphs = """
        Intro:

        Paragraph one content stays here.

        More details follow in paragraph two.

        Second section:

        Another part of the document lives here.

        Final paragraph that ends the content.
        """;

        var planner = new FakePlanner(new[]
        {
            new ChunkPlanItem(new[] { 0, 1, 2 }),
            new ChunkPlanItem(new[] { 3, 4, 5 })
        });

        var chunker = new AiSemanticTextChunker(planner, new AiChunkerOptions
        {
            OverlapSentences = 0
        });

        var chunks = chunker.Chunk("doc", paragraphs).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.Equal("doc::chunk::00000", chunks[0].Id);
        Assert.StartsWith("Section:", chunks[0].Text);
    }

    [Fact]
    public void Falls_back_on_planner_error()
    {
        var text = """
        Heading:
        One two three four five six words here.

        Seven eight nine ten eleven twelve words again.

        Thirteen fourteen fifteen sixteen seventeen eighteen words.
        """;

        var planner = new FakePlanner(exception: new InvalidOperationException("bad json"));
        var options = new AiChunkerOptions
        {
            TargetWords = 5,
            MaxWords = 10,
            MinWords = 1,
            OverlapSentences = 0,
            FallbackToRuleBasedOnError = true
        };

        var chunker = new AiSemanticTextChunker(planner, options);

        var chunks = chunker.Chunk("doc", text).ToList();

        Assert.True(chunks.Count >= 2);
    }

    [Fact]
    public void Falls_back_when_missing_paragraph()
    {
        var text = """
        A.

        B.

        C.
        """;

        var planner = new FakePlanner(new[]
        {
            new ChunkPlanItem(new[] { 0 })
        });

        var options = new AiChunkerOptions
        {
            TargetWords = 1,
            MaxWords = 2,
            MinWords = 1,
            OverlapSentences = 0,
            FallbackToRuleBasedOnError = true
        };

        var chunker = new AiSemanticTextChunker(planner, options);

        var chunks = chunker.Chunk("doc", text).ToList();

        Assert.Equal(3, chunks.Count);
    }

    [Fact]
    public void Falls_back_on_duplicate_index()
    {
        var text = """
        First.

        Second.
        """;

        var planner = new FakePlanner(new[]
        {
            new ChunkPlanItem(new[] { 0, 0 })
        });

        var options = new AiChunkerOptions
        {
            TargetWords = 1,
            MaxWords = 2,
            MinWords = 1,
            OverlapSentences = 0,
            FallbackToRuleBasedOnError = true
        };

        var chunker = new AiSemanticTextChunker(planner, options);

        var chunks = chunker.Chunk("doc", text).ToList();

        Assert.Equal(2, chunks.Count);
    }

    [Fact]
    public void Adds_overlap_between_chunks()
    {
        var text = """
        Header:
        First sentence in the first paragraph. Second sentence sticks around.

        Third paragraph is here to form another chunk.
        """;

        var planner = new FakePlanner(new[]
        {
            new ChunkPlanItem(new[] { 0 }),
            new ChunkPlanItem(new[] { 1 })
        });

        var options = new AiChunkerOptions
        {
            OverlapSentences = 1
        };

        var chunker = new AiSemanticTextChunker(planner, options);
        var chunks = chunker.Chunk("doc", text).ToList();

        Assert.Equal(2, chunks.Count);

        var firstBody = chunks[0].Text;
        var lastSentence = "Second sentence sticks around.";
        Assert.Contains(lastSentence, firstBody);

        var secondBody = chunks[1].Text;
        Assert.Contains(lastSentence, secondBody);
    }

    private sealed class FakePlanner : IChunkPlanner
    {
        private readonly IReadOnlyList<ChunkPlanItem>? _plan;
        private readonly Exception? _exception;

        public FakePlanner(IReadOnlyList<ChunkPlanItem>? plan = null, Exception? exception = null)
        {
            _plan = plan;
            _exception = exception;
        }

        public Task<ChunkPlan> PlanAsync(IReadOnlyList<Paragraph> paragraphs, AiChunkerOptions options, CancellationToken ct = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(new ChunkPlan(_plan ?? Array.Empty<ChunkPlanItem>()));
        }
    }
}
