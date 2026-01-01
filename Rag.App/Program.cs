using System.Text;
using System.Text.Json;
using Rag.Core.Chunking;
using Rag.Core.Services;
using Rag.Ollama;
using Rag.VectorStores.InMemory;

Console.OutputEncoding = Encoding.UTF8;

var config = LoadConfig();
var defaultOptions = new OllamaOptions();

var ollamaOptions = new OllamaOptions
{
    BaseUrl = !string.IsNullOrWhiteSpace(config?.Ollama?.BaseUrl)
        ? new Uri(config.Ollama.BaseUrl)
        : defaultOptions.BaseUrl,
    EmbeddingModel = !string.IsNullOrWhiteSpace(config?.Ollama?.EmbeddingModel)
        ? config!.Ollama!.EmbeddingModel!
        : defaultOptions.EmbeddingModel,
    ChatModel = !string.IsNullOrWhiteSpace(config?.Ollama?.ChatModel)
        ? config!.Ollama!.ChatModel!
        : defaultOptions.ChatModel
};

var topK = config?.Retrieval?.TopK ?? 5;
var threshold = config?.Retrieval?.Threshold ?? 0.72;
var chunkWordCount = config?.Chunking?.ChunkWordCount ?? 180;
var overlapWordCount = config?.Chunking?.OverlapWordCount ?? 40;

using var httpClient = HttpClientFactory.Create(ollamaOptions);

var chunker = new WordChunker(chunkWordCount, overlapWordCount);
var embeddingProvider = new OllamaEmbeddingProvider(httpClient, ollamaOptions);
var vectorStore = new InMemoryVectorStore();
var indexer = new VectorIndexer(chunker, embeddingProvider, vectorStore);
var retriever = new VectorRetriever(embeddingProvider, vectorStore);
var llmClient = new OllamaLlmClient(httpClient, ollamaOptions);
var ragService = new RagService(retriever, llmClient, topK, threshold);

Console.WriteLine("Indexing sample documents...");
await IndexSamplesAsync(indexer);

Console.WriteLine($"Ready. Ollama: {ollamaOptions.BaseUrl}. TopK={topK}, threshold={threshold}.");
Console.WriteLine("Ask a question (empty line to exit).");

while (true)
{
    Console.Write("> ");
    var question = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(question))
    {
        break;
    }

    try
    {
        var answer = await ragService.AskAsync(question);
        Console.WriteLine(answer);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Request failed: {ex.Message}");
    }
}

/// <summary>
/// Indexes embedded sample documents for demonstration.
/// </summary>
static async Task IndexSamplesAsync(VectorIndexer indexer)
{
    var samples = new[]
    {
        new
        {
            Id = "doc-ollama",
            Text = """
            Ollama is a local LLM server that listens on http://localhost:11434 by default.
            Run `ollama pull llama3.1` to download the chat model, and `ollama pull nomic-embed-text` to add the embedding model.
            The HTTP API accepts requests to /api/chat and /api/embeddings.
            """
        },
        new
        {
            Id = "doc-rag",
            Text = """
            Retrieval Augmented Generation (RAG) combines retrieval and generation.
            The process includes chunking text, computing embeddings, storing them in a vector store, and searching via cosine similarity.
            At query time, a TopK search with a score threshold selects context, which is then passed to the LLM for the answer.
            """
        },
        new
        {
            Id = "doc-dotnet",
            Text = """
            This sample application is a .NET 8 console app.
            Start it with `dotnet run --project Rag.App`.
            Entering an empty line stops the program.
            """
        }
    };

    foreach (var sample in samples)
    {
        await indexer.IndexTextAsync(sample.Id, sample.Text);
    }
}

/// <summary>
/// Loads application configuration from appsettings.json if present.
/// </summary>
static AppConfig? LoadConfig()
{
    const string fileName = "appsettings.json";
    var path = Path.Combine(AppContext.BaseDirectory, fileName);

    if (!File.Exists(path))
    {
        return null;
    }

    var json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });
}

/// <summary>
/// Root configuration object for the console app.
/// </summary>
internal sealed class AppConfig
{
    public OllamaConfig? Ollama { get; init; }

    public RetrievalConfig? Retrieval { get; init; }

    public ChunkingConfig? Chunking { get; init; }
}

/// <summary>
/// Ollama connectivity and model selection config.
/// </summary>
internal sealed class OllamaConfig
{
    public string? BaseUrl { get; init; }

    public string? EmbeddingModel { get; init; }

    public string? ChatModel { get; init; }
}

/// <summary>
/// Retrieval configuration.
/// </summary>
internal sealed class RetrievalConfig
{
    public int? TopK { get; init; }

    public double? Threshold { get; init; }
}

/// <summary>
/// Chunking configuration.
/// </summary>
internal sealed class ChunkingConfig
{
    public int? ChunkWordCount { get; init; }

    public int? OverlapWordCount { get; init; }
}
