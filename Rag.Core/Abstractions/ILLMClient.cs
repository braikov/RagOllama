namespace Rag.Core.Abstractions;

/// <summary>
/// Sends prompts to an LLM and returns responses.
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Sends a prompt and returns the model answer.
    /// </summary>
    Task<string> AskAsync(string prompt, CancellationToken ct = default);
}
