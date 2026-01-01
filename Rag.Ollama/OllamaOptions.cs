namespace Rag.Ollama;

/// <summary>
/// Configuration for connecting to an Ollama server and selecting models.
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// Base URL of the Ollama server.
    /// </summary>
    public Uri BaseUrl { get; init; } = new("http://localhost:11434");

    /// <summary>
    /// Embedding model name to request from Ollama.
    /// </summary>
    public string EmbeddingModel { get; init; } = "nomic-embed-text";

    /// <summary>
    /// Chat model name to request from Ollama.
    /// </summary>
    public string ChatModel { get; init; } = "llama3.1";
}
