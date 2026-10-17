# 01: Create and validate a collection

Article: [Create and validate a collection](https://www.lukaswalter.dev/posts/your-first-qdrant-collection/).

Learn to create and validate a collection with a complete, independent console program.

## Run from the repository root

Start Qdrant with `docker compose up -d`. See the [root setup](../../README.md) for prerequisites and provider configuration.

The following commands work in PowerShell and Bash/Zsh.

```shell
dotnet run --project samples/01-first-collection/azure-openai/Sample.csproj
dotnet run --project samples/01-first-collection/ollama/Sample.csproj
```

## Configuration

| Setting | Azure OpenAI | Ollama |
| --- | --- | --- |
| Collection (fixed, isolated) | `blog-samples-01-azure-openai-v1` | `blog-samples-01-ollama-v1` |
| Profile | `support-text-v1` | `support-embeddinggemma-v1` |
| Model | `text-embedding-3-small` | `embeddinggemma:300m` |
| Vector | 1,536-dimensional cosine | 768-dimensional cosine |

`QDRANT_HOST` defaults to `127.0.0.1`, and `QDRANT_GRPC_PORT` defaults to `6334`.

## Expected result

Both variants print a Green collection with cosine distance, the expected dimensions and profile ID. No points are written.

## Preparation and article differences

This sample only creates or validates the collection. The provider folder selects a schema. Neither provider is contacted. No Azure variables or running Ollama server are needed.

The article's `support-documents-v1` and `support-documents-embeddinggemma-v1` names are replaced with the fixed per-sample names above. The embedding profiles, model formatting, fixtures, package versions and topic APIs are preserved. Each Program.cs contains all its own types. No sample references another project or a shared code library.

Existing collections are validated for an unnamed dense vector, dimensions, cosine distance and declared profile. Validation fails if any of these checks do not match. Collections are never automatically deleted. These programs assume one setup process, as the articles do.

