using System.Text;
using Rag.Core.Abstractions;

namespace Rag.Core.Services;

/// <summary>
/// End-to-end RAG orchestrator that retrieves context and asks the LLM.
/// </summary>
public class RagService
{
    private readonly VectorRetriever _retriever;
    private readonly ILLMClient _llm;
    private readonly int _defaultTopK;
    private readonly double _defaultThreshold;

    /// <summary>
    /// Creates a RAG service with retrieval defaults.
    /// </summary>
    public RagService(VectorRetriever retriever, ILLMClient llm, int defaultTopK = 5, double defaultThreshold = 0.72)
    {
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _defaultTopK = defaultTopK;
        _defaultThreshold = defaultThreshold;
    }

    /// <summary>
    /// Answers a question using retrieved context and an LLM with a strict context-only policy.
    /// </summary>
    public async Task<string> AskAsync(string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return "Empty question.";
        }

        var retrieved = await _retriever.RetrieveAsync(question, _defaultTopK, _defaultThreshold, ct).ConfigureAwait(false);
        if (retrieved.Count == 0)
        {
            return "No context found -> I don't know.";
        }

        var contextBuilder = new StringBuilder();
        foreach (var chunk in retrieved)
        {
            contextBuilder.AppendLine($"[score:{chunk.Score:F4}] {chunk.Text}");
        }

        var prompt = $"""
        Use only the context below. If the context is missing or insufficient, answer with "I don't know".
        Context:
        {contextBuilder}

        Question:
        {question}
        Answer:
        """;

        Console.WriteLine($"Prompt: {prompt}");

        return await _llm.AskAsync(prompt, ct).ConfigureAwait(false);
    }
}
