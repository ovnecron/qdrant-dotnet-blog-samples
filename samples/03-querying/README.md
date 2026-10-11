# 03: Query with a document allowlist

Article: [Query with a document allowlist](https://www.lukaswalter.dev/posts/querying-qdrant-from-dotnet/).

Learn to query with a document allowlist with a complete, independent console program.

## Run from the repository root

Start Qdrant with `docker compose up -d`. See the [root setup](../../README.md) for prerequisites and provider configuration.

```bash
dotnet run --project samples/03-querying/azure-openai/Sample.csproj
dotnet run --project samples/03-querying/ollama/Sample.csproj
```

## Configuration

| Setting | Azure OpenAI | Ollama |
| --- | --- | --- |
| Collection (fixed, isolated) | `blog-samples-03-azure-openai-v1` | `blog-samples-03-ollama-v1` |
| Profile | `support-text-v1` | `support-embeddinggemma-v1` |
| Model | `text-embedding-3-small` | `embeddinggemma:300m` |
| Vector | 1,536-dimensional cosine | 768-dimensional cosine |

`QDRANT_HOST` defaults to `127.0.0.1`, and `QDRANT_GRPC_PORT` defaults to `6334`.

Azure requires `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` and `AZURE_OPENAI_API_KEY`. Use an Azure resource endpoint and a deployment of text-embedding-3-small with its default dimensions. Ollama uses `OLLAMA_ENDPOINT` (default `http://localhost:11434`) and requires `ollama pull embeddinggemma:300m`.

## Expected result

The shutdown question and at most two results: worker-shutdown and bounded-work-queue. http-client-lifetime is excluded. worker-shutdown should rank first. Exact scores and ranks depend on the model.

## Preparation and article differences

The program creates or validates its own collection and embeds/upserts/reads back all three notes before querying. This seed step is preparation, separate from the query topic. You do not need to run sample 02.

The article's `support-documents-v1` and `support-documents-embeddinggemma-v1` names are replaced with the fixed per-sample names above. The embedding profiles, model formatting, fixtures, package versions and topic APIs are preserved. Each Program.cs contains all its own types. No sample references another project or a shared code library.

Existing collections are validated for an unnamed dense vector, dimensions, cosine distance and declared profile. Validation fails if any of these checks do not match. Collections are never automatically deleted. These programs assume one setup process, as the articles do.

Stable point IDs and `wait: true` make repeated upserts inspectable. Repeat runs generate embeddings again. Azure calls incur usage charges. Upserts do not remove obsolete points or make multi-point updates atomic. Profile metadata does not prove model-artifact identity. Record the Ollama model digest when comparing results because the fixed tag is mutable. The article baseline retains Ollama's truncation default for this controlled short text.
