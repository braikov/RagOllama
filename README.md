# RagOllama (.NET 8 RAG + Ollama)

Minimal RAG pipeline with chunking, embeddings, in-memory vector store, and chat via Ollama.

## Setup
- Install/start Ollama (`ollama serve`) and pull models:
  - `ollama pull llama3.1`
  - `ollama pull nomic-embed-text`

## Run
- `dotnet run --project Rag.App`
- Optionally edit `Rag.App/appsettings.json` (baseUrl, models, TopK, threshold, chunk size). It is copied to bin and loaded automatically.
- Example questions:
  - `How do I start Ollama?`
  - `What is Retrieval Augmented Generation?`
  - `How do I run the console app?`

## Architecture
- `Rag.Core`: abstractions, models, vector math, and services for indexing/retrieval.
  - Also includes the default `WordChunker`.
- `Rag.VectorStores.InMemory`: in-memory `IVectorStore`.
- `Rag.Ollama`: clients for embeddings and chat against the Ollama HTTP API.
- `Rag.App`: console composition root + sample indexing.

RAG flow: chunking → embeddings → in-memory vector store → TopK + threshold retrieval (cosine) → context to LLM with policy “use only the context; otherwise answer 'I don't know'”.
