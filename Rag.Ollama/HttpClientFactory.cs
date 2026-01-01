using System.Net.Http;

namespace Rag.Ollama;

/// <summary>
/// Helper to create HttpClient instances configured for Ollama.
/// </summary>
public static class HttpClientFactory
{
    /// <summary>
    /// Creates an HttpClient with the base address from options.
    /// </summary>
    public static HttpClient Create(OllamaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var client = new HttpClient
        {
            BaseAddress = options.BaseUrl
        };

        return client;
    }
}
