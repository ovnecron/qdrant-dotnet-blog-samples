# 04: Chunk along section boundaries

Article: [Chunk along section boundaries](https://www.lukaswalter.dev/posts/chunking-before-it-becomes-a-quality-problem/).

Learn to chunk along section boundaries with a complete, independent console program.

## Run from the repository root

See the [root setup](../../README.md) for prerequisites and provider configuration. The default preview runs without Qdrant or an embedding provider.

```bash
dotnet run --project samples/04-chunking/azure-openai/Sample.csproj
dotnet run --project samples/04-chunking/ollama/Sample.csproj
```

Both commands default to preview mode. To index and query instead, start Qdrant with `docker compose up -d` and configure the selected embedding provider, then run:

```bash
PREVIEW_ONLY=false dotnet run --project samples/04-chunking/azure-openai/Sample.csproj
PREVIEW_ONLY=false dotnet run --project samples/04-chunking/ollama/Sample.csproj
```

## Configuration

| Setting | Azure OpenAI | Ollama |
| --- | --- | --- |
| Collection (fixed, isolated) | `blog-samples-04-azure-openai-v1` | `blog-samples-04-ollama-v1` |
| Profile | `support-text-v1` | `support-embeddinggemma-v1` |
| Model | `text-embedding-3-small` | `embeddinggemma:300m` |
| Vector | 1,536-dimensional cosine | 768-dimensional cosine |

`QDRANT_HOST` defaults to `127.0.0.1`, and `QDRANT_GRPC_PORT` defaults to `6334`.

For index/query mode, Azure requires `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` and `AZURE_OPENAI_API_KEY`. Use an Azure resource endpoint and a deployment of text-embedding-3-small with its default dimensions. Ollama uses `OLLAMA_ENDPOINT` (default `http://localhost:11434`) and requires `ollama pull embeddinggemma:300m`.

`PREVIEW_ONLY` defaults to `true`. Set it to `false` to index/query. It must be a boolean.

## Expected result

Preview prints shutdown/p0, shutdown/p1 and capacity/p0, with 80 words or fewer per chunk. Index mode writes and reads back three points, then queries for the shutdown deadline condition.

## Preparation and article differences

The original source sections and Chunker are included in this Program.cs. Preview exits before constructing clients or reading credentials. Index mode creates or validates its collection before ingestion. You do not need any earlier sample.

The article's `support-documents-v1` and `support-documents-embeddinggemma-v1` names are replaced with the fixed per-sample names above. The embedding profiles, model formatting, fixtures, package versions and topic APIs are preserved. Each Program.cs contains all its own types. No sample references another project or a shared code library.

Existing collections are validated for an unnamed dense vector, dimensions, cosine distance and declared profile. Validation fails if any of these checks do not match. Collections are never automatically deleted. These programs assume one setup process, as the articles do.

Stable point IDs and `wait: true` make repeated upserts inspectable. Repeat runs generate embeddings again. Azure calls incur usage charges. Upserts do not remove obsolete points or make multi-point updates atomic. Profile metadata does not prove model-artifact identity. Record the Ollama model digest when comparing results because the fixed tag is mutable. The article baseline retains Ollama's truncation default for this controlled short text.
