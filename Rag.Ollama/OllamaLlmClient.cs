using System.Net.Http.Json;
using Rag.Core.Abstractions;

namespace Rag.Ollama;

/// <summary>
/// LLM client that sends chat prompts to Ollama.
/// </summary>
public class OllamaLlmClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    /// <summary>
    /// Creates a new client targeting the configured chat model.
    /// </summary>
    public OllamaLlmClient(HttpClient httpClient, OllamaOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(options);

        _model = string.IsNullOrWhiteSpace(options.ChatModel) ? "llama3.1" : options.ChatModel;
    }

    /// <summary>
    /// Sends a user prompt to Ollama and returns the response text.
    /// </summary>
    public async Task<string> AskAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            stream = false
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/chat", payload, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct).ConfigureAwait(false);
        return result?.Message?.Content ?? string.Empty;
    }

    private sealed class ChatResponse
    {
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; set; }
    }
}
