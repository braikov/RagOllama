namespace Rag.Core.Chunking.AiChunker;

/// <summary>
/// Configuration for AI-driven semantic chunking.
/// </summary>
public sealed class AiChunkerOptions
{
    public string Model { get; init; } = "qwen2.5:14b-instruct";

    public int TimeoutMs { get; init; } = 60_000;

    public int MaxParagraphsPerRequest { get; init; } = 80;

    public int MaxParagraphChars { get; init; } = 1_200;

    public int TargetWords { get; init; } = 700;

    public int MinWords { get; init; } = 200;

    public int MaxWords { get; init; } = 1_100;

    public int OverlapSentences { get; init; } = 2;

    public bool IncludeHeaderPrefix { get; init; } = true;

    public string HeaderPrefixFormat { get; init; } = "Section: {path}\n\n";

    public bool FallbackToRuleBasedOnError { get; init; } = true;

    public PromptOptions Prompt { get; init; } = new();

    public sealed class PromptOptions
    {
        public string System { get; init; } = "You are a text segmentation engine. You never rewrite text. You only group paragraphs into ordered chunks. Return ONLY valid JSON.";

        public IReadOnlyList<string> UserTemplate { get; init; } = new List<string>
        {
            "Group the paragraphs into coherent chunks for RAG retrieval.",
            "",
            "CONSTRAINTS:",
            "- Keep original paragraph order.",
            "- Use each paragraph exactly once.",
            "- Do not rewrite paragraph text.",
            "- Prefer splitting on topic changes and headings.",
            "- If a paragraph looks like a heading, keep it with the following content.",
            "- Target chunk size: {{targetWords}} words, min {{minWords}}, max {{maxWords}} (approximate).",
            "",
            "RETURN JSON ONLY in this schema:",
            "{ \"chunks\": [ { \"paragraphs\": [0,1,2], \"title\": \"optional\" } ] }",
            "",
            "PARAGRAPHS:",
            "{{paragraphs}}"
        };
    }
}
