# RagOllama Architecture Spec

## Overview
Minimal .NET 8 RAG pipeline split into four projects:
- `Rag.Core` — pure abstractions, domain models, math utilities, and services for indexing/retrieval/RAG orchestration.
- `Rag.VectorStores.InMemory` — in-memory `IVectorStore` implementation using cosine similarity.
- `Rag.Ollama` — HTTP clients for Ollama embeddings and chat.
- `Rag.App` — console composition root that wires components, indexes sample documents, and answers questions.

## Data Flow
1) **Chunking**: `ITextChunker` (default `WordChunker`) splits source text into overlapping word chunks, emitting `TextChunk` records with stable IDs (`{sourceId}::chunk::{index}`).
2) **Embeddings**: `IEmbeddingProvider` (Ollama) converts chunk text to vectors.
3) **Indexing**: `VectorIndexer` pairs chunks with embeddings into `VectorRecord` instances and upserts them into the vector store.
4) **Retrieval**: `VectorRetriever` embeds the query, runs `IVectorStore.SearchAsync` with TopK + threshold over cosine similarity, returning `RetrievedChunk`.
5) **Generation**: `RagService` builds a context block (includes scores for debugging) and asks the LLM with a policy: “use only the context; otherwise answer 'I don't know'”.

## Key Abstractions (Rag.Core)
- `ITextChunker`: `Chunk(string sourceId, string text) -> IEnumerable<TextChunk>`
- `IEmbeddingProvider`: `EmbedAsync(string text, CancellationToken)`
- `IVectorStore`: `UpsertAsync(IEnumerable<VectorRecord>)`, `SearchAsync(float[] queryVector, int topK, double threshold, CancellationToken)`
- `ILLMClient`: `AskAsync(string prompt, CancellationToken)`
- Models: `TextChunk`, `VectorRecord`, `RetrievedChunk`
- Math: `VectorMath.CosineSimilarity`
- Services: `VectorIndexer`, `VectorRetriever`, `RagService`
- Chunker impl: `WordChunker` (configurable chunk size and overlap)

## Implementations
- **Vector store**: `InMemoryVectorStore` keeps an internal list, upserts by `Id`, and ranks by cosine similarity; filters by threshold then takes TopK.
- **Ollama clients**:
  - Embeddings: `POST /api/embeddings` `{ model, prompt }` → `embedding: float[]`
  - Chat: `POST /api/chat` `{ model, messages:[{role:"user", content:prompt}], stream:false }` → `message.content`
  - Options: `OllamaOptions` (BaseUrl, EmbeddingModel, ChatModel), `HttpClientFactory` sets `BaseAddress`.

## Console App Composition (Rag.App)
- Loads optional `appsettings.json` (BaseUrl, models, TopK, threshold, chunk sizes).
- Builds `HttpClient` with Ollama options.
- Constructs `WordChunker`, `OllamaEmbeddingProvider`, `InMemoryVectorStore`, `VectorIndexer`, `VectorRetriever`, `OllamaLlmClient`, `RagService`.
- On start: indexes bundled sample documents.
- REPL: reads question; empty line exits; prints answer or error.

## Config Defaults
- BaseUrl: `http://localhost:11434`
- Embedding model: `nomic-embed-text`
- Chat model: `llama3.1`
- Retrieval: TopK=5, threshold=0.72
- Chunking: 180 words with 40-word overlap

## Constraints
- Core project has no dependencies on implementations.
- Similarity: cosine similarity via `VectorMath`.
- Policy: responses must rely on retrieved context; otherwise respond with “I don't know”.
