# RAG with Ollama – Architecture Specification

## 1. Overview

This project implements a **Retrieval-Augmented Generation (RAG)** architecture in C#, using **Ollama** as a local Large Language Model (LLM) and embedding provider.

The system does **not** train or modify the LLM. Instead, it retrieves relevant information from an external vector store at query time and injects it as context into the prompt.

This architecture is designed to be:
- clean and minimal
- production-oriented
- fully replaceable at every infrastructure layer

---

## 2. High-Level Architecture

```
┌───────────────┐
│   Data Source │  (files, DB, wiki, etc.)
└───────┬───────┘
        │
        ▼
┌────────────────────┐
│  Text Chunker      │  (ITextChunker)
└───────┬────────────┘
        │  chunks
        ▼
┌────────────────────┐
│ Embedding Provider │  (IEmbeddingProvider)
│     (Ollama)       │
└───────┬────────────┘
        │  vectors
        ▼
┌────────────────────┐
│   Vector Store     │  (IVectorStore)
│  (In-Memory impl)  │
└───────┬────────────┘
        │
        ▼
┌────────────────────┐
│ Vector Retriever   │  (Top-K + Threshold)
└───────┬────────────┘
        │  relevant chunks
        ▼
┌────────────────────┐
│  Prompt Assembly   │
└───────┬────────────┘
        │
        ▼
┌────────────────────┐
│     LLM Chat       │  (ILLMClient / Ollama)
└────────────────────┘
```

---

## 3. Design Principles

### 3.1 Separation of Abstractions and Implementations

All abstractions live in **Rag.Core**:
- interfaces
- domain models
- pure logic

Concrete implementations live in dedicated projects:
- `Rag.Ollama`
- `Rag.VectorStores.InMemory`

This ensures that:
- Core has zero infrastructure dependencies
- Any component can be swapped without refactoring

---

### 3.2 Stateless LLM

The LLM:
- does not store knowledge
- does not learn from requests
- does not persist embeddings

All knowledge is provided **explicitly** via context injection.

---

### 3.3 Replaceability

Each major component is replaceable:
- Chunker (word-based → token-based)
- Embedding provider (Ollama → OpenAI → local model)
- Vector store (InMemory → FAISS → Qdrant)
- LLM client (Ollama → vLLM → OpenAI)

---

## 4. Project Structure

### 4.1 Rag.Core

**Purpose:** domain logic and abstractions only.

Contains:

#### Interfaces
- `ITextChunker`
- `IEmbeddingProvider`
- `IVectorStore`
- `ILLMClient`

#### Models
- `TextChunk`
- `VectorRecord`
- `RetrievedChunk`

#### Services
- `VectorIndexer`
- `VectorRetriever`
- `RagService`

#### Utilities
- `VectorMath` (cosine similarity)

No references to:
- HTTP
- Ollama
- databases
- filesystem

---

### 4.2 Rag.VectorStores.InMemory

**Purpose:** reference vector store implementation.

Characteristics:
- vectors stored in memory
- cosine similarity ranking
- Top-K + threshold filtering

Used for:
- demos
- tests
- baseline correctness

---

### 4.3 Rag.Ollama

**Purpose:** Ollama integration.

Contains:
- `OllamaEmbeddingProvider`
- `OllamaLlmClient`
- `OllamaOptions`

Uses:
- `/api/embeddings`
- `/api/chat`

---

### 4.4 Rag.App

**Purpose:** composition root and CLI interface.

Responsibilities:
- wire dependencies
- index initial documents
- read questions from stdin
- print answers

Only executable project.

---

## 5. Data Flow

### 5.1 Indexing Flow

```
Raw Text
  ↓
Chunking
  ↓
Embedding
  ↓
VectorRecord
  ↓
Upsert into Vector Store
```

---

### 5.2 Query Flow

```
User Question
  ↓
Embedding
  ↓
Similarity Search
  ↓
Ranking (score desc)
  ↓
Top-K + Threshold
  ↓
Context Assembly
  ↓
LLM Prompt
  ↓
Answer
```

---

## 6. Similarity Scoring

### Metric
- Cosine similarity

### Interpretation
- 1.0   → identical meaning
- 0.8+  → highly relevant
- 0.7+  → acceptable
- <0.6  → discarded

### Strategy
- sort by similarity descending
- take Top-K
- apply similarity threshold

This avoids prompt pollution and hallucinations.

---

## 7. Prompt Policy

The LLM must:
- use **only** the provided context
- explicitly answer "Не знам" if context is insufficient
- never rely on internal knowledge

This ensures deterministic behavior.

---

## 8. Non-Goals

This architecture intentionally excludes:
- fine-tuning
- conversational memory
- cross-encoder reranking
- authentication / authorization
- distributed vector search

---

## 9. Extensibility Roadmap

Possible extensions:
- persistent vector stores
- document loaders (files, MediaWiki, SQL)
- LLM-based reranking
- streaming responses
- source citations

---

## 10. Summary

This project provides a **clean, minimal, production-grade RAG foundation** with strict separation of concerns and full replaceability.

RAG = Retrieval + Context + Generation
