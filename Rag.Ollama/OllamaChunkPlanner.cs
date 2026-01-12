using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Rag.Core.Chunking.AiChunker;

namespace Rag.Ollama;

/// <summary>
/// Ollama-backed chunk planner that delegates grouping decisions to an LLM via /api/chat.
/// </summary>
public sealed class OllamaChunkPlanner : IChunkPlanner
{
    private readonly HttpClient _httpClient;

    public OllamaChunkPlanner(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ChunkPlan> PlanAsync(IReadOnlyList<Paragraph> paragraphs, AiChunkerOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        ArgumentNullException.ThrowIfNull(options);

        if (paragraphs.Count == 0)
        {
            throw new InvalidOperationException("No paragraphs to plan.");
        }

        if (options.MaxParagraphsPerRequest > 0 && paragraphs.Count > options.MaxParagraphsPerRequest)
        {
            throw new InvalidOperationException("Paragraph count exceeds MaxParagraphsPerRequest.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (options.TimeoutMs > 0)
        {
            cts.CancelAfter(options.TimeoutMs);
        }

        var userPrompt = BuildUserPrompt(paragraphs, options);
        var payload = new
        {
            model = options.Model,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = options.Prompt.System },
                new { role = "user", content = userPrompt }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/chat", payload, cts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cts.Token).ConfigureAwait(false);
        var content = result?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Planner returned empty content.");
        }

        ChunkPlanResponse? plan;
        try
        {
            plan = JsonSerializer.Deserialize<ChunkPlanResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Planner returned invalid JSON.", ex);
        }

        if (plan?.Chunks is null || plan.Chunks.Count == 0)
        {
            throw new InvalidOperationException("Planner returned no chunks.");
        }

        var items = plan.Chunks
            .Select(c => new ChunkPlanItem(c.Paragraphs ?? new List<int>(), c.Title))
            .ToList();
        return new ChunkPlan(items);
    }

    private static string BuildUserPrompt(IReadOnlyList<Paragraph> paragraphs, AiChunkerOptions options)
    {
        var paragraphsBlock = FormatParagraphs(paragraphs, options.MaxParagraphChars);
        var template = string.Join("\n", options.Prompt.UserTemplate);

        return template
            .Replace("{{targetWords}}", options.TargetWords.ToString())
            .Replace("{{minWords}}", options.MinWords.ToString())
            .Replace("{{maxWords}}", options.MaxWords.ToString())
            .Replace("{{paragraphs}}", paragraphsBlock);
    }

    private static string FormatParagraphs(IReadOnlyList<Paragraph> paragraphs, int maxChars)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var text = paragraphs[i].Text;
            if (maxChars > 0 && text.Length > maxChars)
            {
                text = text[..maxChars];
            }

            sb.Append("p").Append(i).Append(": \"\"\"").Append(text).AppendLine("\"\"\"");
        }

        return sb.ToString();
    }

    private sealed class ChatResponse
    {
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; set; }
    }

    private sealed class ChunkPlanResponse
    {
        public List<ChunkPlanItemDto>? Chunks { get; set; }
    }

    private sealed class ChunkPlanItemDto
    {
        public List<int>? Paragraphs { get; set; }
        public string? Title { get; set; }
    }
}
