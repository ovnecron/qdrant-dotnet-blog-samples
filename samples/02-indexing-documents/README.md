# 02: Index and read back documents

Article: [Index and read back documents](https://www.lukaswalter.dev/posts/indexing-your-first-documents/).

Learn to index and read back documents with a complete, independent console program.

## Run from the repository root

Start Qdrant with `docker compose up -d`. See the [root setup](../../README.md) for prerequisites and provider configuration.

The following commands work in PowerShell and Bash/Zsh.

```shell
dotnet run --project samples/02-indexing-documents/azure-openai/Sample.csproj
dotnet run --project samples/02-indexing-documents/ollama/Sample.csproj
```

## Configuration

| Setting | Azure OpenAI | Ollama |
| --- | --- | --- |
| Collection (fixed, isolated) | `blog-samples-02-azure-openai-v1` | `blog-samples-02-ollama-v1` |
| Profile | `support-text-v1` | `support-embeddinggemma-v1` |
| Model | `text-embedding-3-small` | `embeddinggemma:300m` |
| Vector | 1,536-dimensional cosine | 768-dimensional cosine |

`QDRANT_HOST` defaults to `127.0.0.1`, and `QDRANT_GRPC_PORT` defaults to `6334`.

Azure requires `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` and `AZURE_OPENAI_API_KEY`. Use an Azure resource endpoint and a deployment of text-embedding-3-small with its default dimensions. Ollama uses `OLLAMA_ENDPOINT` (default `http://localhost:11434`) and requires `ollama pull embeddinggemma:300m`.

## Expected result

Three `Indexed ...` messages and successful read-back checks for worker-shutdown, http-client-lifetime and bounded-work-queue. Repeating the run leaves three points in a fresh sample collection.

## Preparation and article differences

Collection creation and contract validation are preparation. The article topic starts with the three-note ingestion loop. The final three-document fixture from the article is used.

The article's `support-documents-v1` and `support-documents-embeddinggemma-v1` names are replaced with the fixed per-sample names above. The embedding profiles, model formatting, fixtures, package versions and topic APIs are preserved. Each Program.cs contains all its own types. No sample references another project or a shared code library.

Existing collections are validated for an unnamed dense vector, dimensions, cosine distance and declared profile. Validation fails if any of these checks do not match. Collections are never automatically deleted. These programs assume one setup process, as the articles do.

Stable point IDs and `wait: true` make repeated upserts inspectable. Repeat runs generate embeddings again. Azure calls incur usage charges. Upserts do not remove obsolete points or make multi-point updates atomic. Profile metadata does not prove model-artifact identity. Record the Ollama model digest when comparing results because the fixed tag is mutable. The article baseline retains Ollama's truncation default for this controlled short text.
