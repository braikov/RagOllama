using System.Net.Http.Json;
using Rag.Core.Abstractions;

namespace Rag.Ollama;

/// <summary>
/// Embedding provider that calls Ollama's embeddings API.
/// </summary>
public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    /// <summary>
    /// Creates a new embedding provider targeting the configured model.
    /// </summary>
    public OllamaEmbeddingProvider(HttpClient httpClient, OllamaOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(options);

        _model = string.IsNullOrWhiteSpace(options.EmbeddingModel) ? "nomic-embed-text" : options.EmbeddingModel;
    }

    /// <summary>
    /// Requests an embedding from Ollama for the given text.
    /// </summary>
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text is required for embeddings.", nameof(text));
        }

        var payload = new { model = _model, prompt = text };

        using var response = await _httpClient.PostAsJsonAsync("/api/embeddings", payload, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct).ConfigureAwait(false);
        if (result?.Embedding is null || result.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Ollama did not return an embedding.");
        }

        return result.Embedding;
    }

    private sealed class EmbeddingResponse
    {
        public float[]? Embedding { get; set; }
    }
}
