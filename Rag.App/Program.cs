using System.Text;
using System.Text.Json;
using Rag.Core.Abstractions;
using Rag.Core.Chunking;
using Rag.Core.Chunking.Adaptive;
using Rag.Core.Chunking.AiChunker;
using Rag.Core.Services;
using Rag.Ollama;
using Rag.VectorStores.InMemory;

internal class Program
{
    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        var config = LoadConfig();
        var defaultOllama = new OllamaOptions();
        var defaultAdaptive = new AdaptiveSectionChunkerOptions();
        var defaultAi = new AiChunkerOptions();

        var ollamaConfig = config?.Ollama;
        var retrievalConfig = config?.Retrieval;
        var chunkingConfig = config?.Chunking;

        var ollamaOptions = new OllamaOptions
        {
            BaseUrl = !string.IsNullOrWhiteSpace(ollamaConfig?.BaseUrl)
                ? new Uri(ollamaConfig.BaseUrl)
                : defaultOllama.BaseUrl,
            EmbeddingModel = !string.IsNullOrWhiteSpace(ollamaConfig?.EmbeddingModel)
                ? ollamaConfig!.EmbeddingModel!
                : defaultOllama.EmbeddingModel,
            ChatModel = !string.IsNullOrWhiteSpace(ollamaConfig?.ChatModel)
                ? ollamaConfig!.ChatModel!
                : defaultOllama.ChatModel
        };

        var topK = retrievalConfig?.TopK ?? 5;
        var threshold = retrievalConfig?.Threshold ?? 0.72;

        using var httpClient = HttpClientFactory.Create(ollamaOptions);

        var chunker = CreateChunker(httpClient, chunkingConfig, defaultAdaptive, defaultAi);
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
    }

    private static ITextChunker CreateChunker(HttpClient httpClient, ChunkingConfig? chunking, AdaptiveSectionChunkerOptions defaultAdaptive, AiChunkerOptions defaultAi)
    {
        var mode = chunking?.Mode ?? "Adaptive";

        if (string.Equals(mode, "AiSemantic", StringComparison.OrdinalIgnoreCase))
        {
            var aiOptions = BuildAiOptions(chunking?.AiSemantic, defaultAi);
            var planner = new OllamaChunkPlanner(httpClient);
            return new AiSemanticTextChunker(planner, aiOptions);
        }

        if (string.Equals(mode, "Word", StringComparison.OrdinalIgnoreCase))
        {
            var word = chunking?.Word;
            var chunkWordCount = word?.ChunkWordCount ?? 180;
            var overlap = word?.OverlapWordCount ?? 40;
            return new WordChunker(chunkWordCount, overlap);
        }

        var adaptiveOptions = BuildAdaptiveOptions(chunking?.Adaptive, defaultAdaptive);
        return new AdaptiveSectionChunker(adaptiveOptions);
    }

    private static AdaptiveSectionChunkerOptions BuildAdaptiveOptions(AdaptiveChunkingConfig? cfg, AdaptiveSectionChunkerOptions defaults)
    {
        cfg ??= new AdaptiveChunkingConfig();

        return new AdaptiveSectionChunkerOptions
        {
            TargetWords = cfg.TargetWords ?? defaults.TargetWords,
            MaxWords = cfg.MaxWords ?? defaults.MaxWords,
            MinWords = cfg.MinWords ?? defaults.MinWords,
            OverlapRatio = cfg.OverlapRatio ?? defaults.OverlapRatio,
            OverlapSentences = cfg.OverlapSentences ?? defaults.OverlapSentences,
            MaxHeaderPrefixChars = cfg.MaxHeaderPrefixChars ?? defaults.MaxHeaderPrefixChars,
            MaxChunkTextCharsForEmbedding = cfg.MaxChunkTextCharsForEmbedding ?? defaults.MaxChunkTextCharsForEmbedding,
            IncludeHeaderPathPrefix = cfg.IncludeHeaderPathPrefix ?? defaults.IncludeHeaderPathPrefix,
            HeaderPrefixFormat = cfg.HeaderPrefixFormat ?? defaults.HeaderPrefixFormat,
            TrimWhitespace = cfg.TrimWhitespace ?? defaults.TrimWhitespace,
            NormalizeWhitespace = cfg.NormalizeWhitespace ?? defaults.NormalizeWhitespace
        };
    }

    private static AiChunkerOptions BuildAiOptions(AiSemanticChunkingConfig? cfg, AiChunkerOptions defaults)
    {
        cfg ??= new AiSemanticChunkingConfig();

        return new AiChunkerOptions
        {
            Model = cfg.Model ?? defaults.Model,
            TimeoutMs = cfg.TimeoutMs ?? defaults.TimeoutMs,
            MaxParagraphsPerRequest = cfg.MaxParagraphsPerRequest ?? defaults.MaxParagraphsPerRequest,
            MaxParagraphChars = cfg.MaxParagraphChars ?? defaults.MaxParagraphChars,
            TargetWords = cfg.TargetWords ?? defaults.TargetWords,
            MinWords = cfg.MinWords ?? defaults.MinWords,
            MaxWords = cfg.MaxWords ?? defaults.MaxWords,
            OverlapSentences = cfg.OverlapSentences ?? defaults.OverlapSentences,
            IncludeHeaderPrefix = cfg.IncludeHeaderPrefix ?? defaults.IncludeHeaderPrefix,
            HeaderPrefixFormat = cfg.HeaderPrefixFormat ?? defaults.HeaderPrefixFormat,
            FallbackToRuleBasedOnError = cfg.FallbackToRuleBasedOnError ?? defaults.FallbackToRuleBasedOnError,
            Prompt = new AiChunkerOptions.PromptOptions
            {
                System = cfg.Prompt?.System ?? defaults.Prompt.System,
                UserTemplate = cfg.Prompt?.UserTemplate?.Count > 0 ? cfg.Prompt.UserTemplate : defaults.Prompt.UserTemplate
            }
        };
    }

    private static async Task IndexSamplesAsync(VectorIndexer indexer)
    {
        var samples = new[]
        {
            new
            {
                Id = "doc-ollama",
                Text = """
                Ollama is a local LLM server that listens on http://localhost:11434 by default.
                in my case, I have it running at http://192.168.0.233:11434.
                Run ollama pull llama3.1 to download the chat model, and ollama pull nomic-embed-text to add the embedding model.
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
                Start it with dotnet run --project Rag.App.
                Entering an empty line stops the program.
                """
            },
            new
            {
                Id = "Enterprise-Carpentry",
                Text = """
                Commercial Projects
                DISPLAYS AND FIXTURES
                Impress your clients with a breathtaking office, showroom or warehouse. CFL Custom Carpentry can help to
                design and build custom product displays, shelving, storage units and anything else you might need to keep
                your business looking a touch above the rest.

                RESTAURANTS AND BARS
                Let CFL Custom Carpentry help you build out a new restaurant or bar, and help you renovate an existing one.
                Our goal is to enhance your customers experience and keep them entertained in a comfortable, entertaining
                and inviting space. Ask us about our custom designed bars, tables, kiosks, decorative walls and more.

                Custom Ceilings
                Add more elegance to your beautiful home with CFL Custom Carpentry creative ceiling designs. Our amazing
                designs include Coffered, Beamed, Layered Cedar, Planked Wood and more.
                Our custom design will reflect your style and home. We can do Light and Bright, Swathed in Cedar, Rugged
                yet Rich, Lofty Design and more. Let us know when you are ready to begin!

                Custom Kitchen Remodeling and Refacing
                The kitchen is probably the most used area in your home. This is where lifetime memories are made with
                family and friends. So you want it to be a space you enjoy spending time and entertaining in. A kitchen design
                you'll love for years to come is our utmost importance.
                So whether you're renovating with new cabinets or simply choosing to spruce up and reface your existing
                ones, we can help you optimize the value of your space, within a practical budget. Let us help you take your
                kitchen from Country Casual to Sleek and Modern-and everything in between. Add gorgeous countertops,
                unique backsplashes, and statement lighting and your kitchen will be completely transformed.

                Services We Do Not Provide:
                We do not paint houses outside.
                Does Not Go More Than: 50mi

                I have over 25 years of experience in residential, commercial and government carpentry. I have worked extensively with the
                most trusted names in the Carpentry field since 1999. Since then, I have mastered many skills including, but not limited to:
                - Flooring (carpet, hardwood, cement)
                - Door/hinge installation (including frame modifications)
                - Drywall work, crown molding
                - Kitchen remodels (Counter tops, Cabinets, Panels, drawers, sky lights)
                - Framing / Insulation
                - Masonry
                - Handicap ramps/handrails
                - Painting/touch ups
                - Mild roof/deck repairs
                - General indoor and outdoor carpentry

                Recent-Experience
                EMBASSY OF IBRAHIM, DC | Private, Government - Current
                - Sole carpenter since 2019. I handle all their carpentry needs and expansions full time
                - Custom built 20x20 custom offsite security guard watch home
                - Repaved 75 feet driveway, embassy perimeter
                - Complete room remodels, bathroom remodels, kitchen remodels
                - Many custom projects, repairs to high end furniture. Emergency leaks and home repairs

                MAHOGANY | Commercial
                - MGM CASINO: Columns, Panels, handrails, bar and casino remodel
                - Tysons Mall: Remolded multiple small stores
                - Fairfax Hospital: Panels, Cabinets, remodeled surgery room
                - City Center Washington DC: Bathroom and restaurant remodels, columns, panels

                WASHINGTON WOODWORKING | Commercial
                - Fairfax hospital: Remolded lobbies, cabinets, panels
                - Giants: Put up isles, crown molding, FRP
                - George Mason: Theater repair, handrails, panels
                - NOVA Alexandria: Handrails, crown molding, panels
                - Bank of America: Reception Desk, panels, cabinet

                (239)205-3031
                Washington, DC, 22311
                """
            }
        };

        foreach (var sample in samples)
        {
            await indexer.IndexTextAsync(sample.Id, sample.Text);
        }
    }

    private static AppConfig? LoadConfig()
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

    private sealed class AppConfig
    {
        public OllamaConfig? Ollama { get; init; }

        public RetrievalConfig? Retrieval { get; init; }

        public ChunkingConfig? Chunking { get; init; }
    }

    private sealed class OllamaConfig
    {
        public string? BaseUrl { get; init; }

        public string? EmbeddingModel { get; init; }

        public string? ChatModel { get; init; }
    }

    private sealed class RetrievalConfig
    {
        public int? TopK { get; init; }

        public double? Threshold { get; init; }
    }

    private sealed class ChunkingConfig
    {
        public string? Mode { get; init; }

        public WordChunkingConfig? Word { get; init; }

        public AdaptiveChunkingConfig? Adaptive { get; init; }

        public AiSemanticChunkingConfig? AiSemantic { get; init; }
    }

    private sealed class WordChunkingConfig
    {
        public int? ChunkWordCount { get; init; }
        public int? OverlapWordCount { get; init; }
    }

    private sealed class AdaptiveChunkingConfig
    {
        public int? TargetWords { get; init; }

        public int? MaxWords { get; init; }

        public int? MinWords { get; init; }

        public double? OverlapRatio { get; init; }

        public int? OverlapSentences { get; init; }

        public int? MaxHeaderPrefixChars { get; init; }

        public int? MaxChunkTextCharsForEmbedding { get; init; }

        public bool? IncludeHeaderPathPrefix { get; init; }

        public string? HeaderPrefixFormat { get; init; }

        public bool? TrimWhitespace { get; init; }

        public bool? NormalizeWhitespace { get; init; }
    }

    private sealed class AiSemanticChunkingConfig
    {
        public string? Model { get; init; }

        public int? TimeoutMs { get; init; }

        public int? MaxParagraphsPerRequest { get; init; }

        public int? MaxParagraphChars { get; init; }

        public int? TargetWords { get; init; }

        public int? MinWords { get; init; }

        public int? MaxWords { get; init; }

        public int? OverlapSentences { get; init; }

        public bool? IncludeHeaderPrefix { get; init; }

        public string? HeaderPrefixFormat { get; init; }

        public bool? FallbackToRuleBasedOnError { get; init; }

        public PromptConfig? Prompt { get; init; }
    }

    private sealed class PromptConfig
    {
        public string? System { get; init; }

        public List<string>? UserTemplate { get; init; }
    }
}
