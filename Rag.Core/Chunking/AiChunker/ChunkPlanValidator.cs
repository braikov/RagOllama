namespace Rag.Core.Chunking.AiChunker;

internal sealed class ChunkPlanValidator
{
    public bool TryValidate(int paragraphCount, ChunkPlan plan, out string error)
    {
        error = string.Empty;

        if (plan is null || plan.Chunks is null)
        {
            error = "Plan is null.";
            return false;
        }

        if (plan.Chunks.Count == 0)
        {
            error = "Plan has no chunks.";
            return false;
        }

        var seen = new bool[paragraphCount];
        var lastIndex = -1;

        for (var i = 0; i < plan.Chunks.Count; i++)
        {
            var chunk = plan.Chunks[i];

            if (chunk.Paragraphs is null || chunk.Paragraphs.Count == 0)
            {
                error = $"Chunk {i} has no paragraphs.";
                return false;
            }

            foreach (var idx in chunk.Paragraphs)
            {
                if (idx < 0 || idx >= paragraphCount)
                {
                    error = $"Paragraph index {idx} is out of range.";
                    return false;
                }

                if (seen[idx])
                {
                    error = $"Paragraph index {idx} is duplicated.";
                    return false;
                }

                if (idx < lastIndex)
                {
                    error = "Paragraph ordering is not monotonic.";
                    return false;
                }

                seen[idx] = true;
                lastIndex = idx;
            }
        }

        if (seen.Any(s => !s))
        {
            error = "At least one paragraph was not assigned.";
            return false;
        }

        return true;
    }
}

public sealed record ChunkPlan(IReadOnlyList<ChunkPlanItem> Chunks);

public sealed record ChunkPlanItem(IReadOnlyList<int> Paragraphs, string? Title = null);
