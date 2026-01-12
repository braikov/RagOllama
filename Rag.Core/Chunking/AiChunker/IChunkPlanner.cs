namespace Rag.Core.Chunking.AiChunker;

/// <summary>
/// Plans semantic chunk boundaries over a set of paragraphs using an LLM or other strategy.
/// </summary>
public interface IChunkPlanner
{
    Task<ChunkPlan> PlanAsync(IReadOnlyList<Paragraph> paragraphs, AiChunkerOptions options, CancellationToken ct = default);
}
