# 05: Model payload and filter by metadata

Article: [Model payload and filter by metadata](https://www.lukaswalter.dev/posts/payload-modeling-and-metadata-filters/).

Learn to model payload and filter by metadata with a complete, independent console program.

## Run from the repository root

See the [root setup](../../README.md) for prerequisites and provider configuration. The default preview runs without Qdrant or an embedding provider.

The following commands work in PowerShell and Bash/Zsh.

```shell
dotnet run --project samples/05-payload-modeling-and-metadata/azure-openai/Sample.csproj
dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
```

Both commands default to preview mode. To index and query instead, start Qdrant with `docker compose up -d` and configure the selected embedding provider, then run:

**Bash/Zsh**

```bash
PREVIEW_ONLY=false dotnet run --project samples/05-payload-modeling-and-metadata/azure-openai/Sample.csproj
PREVIEW_ONLY=false dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
```

**PowerShell**

```powershell
$env:PREVIEW_ONLY = "false"
try {
    dotnet run --project samples/05-payload-modeling-and-metadata/azure-openai/Sample.csproj
    dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
}
finally {
    Remove-Item Env:PREVIEW_ONLY -ErrorAction SilentlyContinue
}
```

## Configuration

| Setting | Azure OpenAI | Ollama |
| --- | --- | --- |
| Collection (fixed, isolated) | `blog-samples-05-azure-openai-v1` | `blog-samples-05-ollama-v1` |
| Profile | `support-text-v1` | `support-embeddinggemma-v1` |
| Model | `text-embedding-3-small` | `embeddinggemma:300m` |
| Vector | 1,536-dimensional cosine | 768-dimensional cosine |

`QDRANT_HOST` defaults to `127.0.0.1`, and `QDRANT_GRPC_PORT` defaults to `6334`.

For index/query mode, Azure requires `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` and `AZURE_OPENAI_API_KEY`. Use an Azure resource endpoint and a deployment of text-embedding-3-small with its default dimensions. Ollama uses `OLLAMA_ENDPOINT` (default `http://localhost:11434`) and requires `ollama pull embeddinggemma:300m`.

`PREVIEW_ONLY` defaults to `true`. Set it to `false` to index/query. It must be a boolean.

The article's manual filter cases are available without editing code:

**Bash/Zsh**

```bash
PREVIEW_ONLY=false REQUIRED_PUBLICATION_STATE=draft dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
PREVIEW_ONLY=false REQUIRED_DOCUMENT_TYPE=note dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
PREVIEW_ONLY=false REQUIRED_DOCUMENT_ID=worker-shutdown-draft dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
```

**PowerShell**

```powershell
$env:PREVIEW_ONLY = "false"
$env:REQUIRED_PUBLICATION_STATE = "draft"
try {
    dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
}
finally {
    Remove-Item Env:PREVIEW_ONLY, Env:REQUIRED_PUBLICATION_STATE -ErrorAction SilentlyContinue
}

$env:PREVIEW_ONLY = "false"
$env:REQUIRED_DOCUMENT_TYPE = "note"
try {
    dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
}
finally {
    Remove-Item Env:PREVIEW_ONLY, Env:REQUIRED_DOCUMENT_TYPE -ErrorAction SilentlyContinue
}

$env:PREVIEW_ONLY = "false"
$env:REQUIRED_DOCUMENT_ID = "worker-shutdown-draft"
try {
    dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
}
finally {
    Remove-Item Env:PREVIEW_ONLY, Env:REQUIRED_DOCUMENT_ID -ErrorAction SilentlyContinue
}
```

`REQUIRED_DOCUMENT_TYPE` defaults to `runbook`, `REQUIRED_PUBLICATION_STATE` to `published`, and `REQUIRED_DOCUMENT_ID` is optional. The cases return the draft runbook, the published note, and no results respectively. Required conditions are never relaxed automatically. Azure supports the same variables.

## Expected result

Preview prints seven chunks: three published-runbook chunks, three draft-runbook chunks and one published note. Index mode writes all seven, checks their payloads, and returns only worker-shutdown chunks for the default published-runbook query.

## Preparation and article differences

The complete chunker, original sections, SourceDocument and PayloadFields helpers are included. Index mode creates or validates its collection and four keyword indexes before ingesting the seven chunks. You do not need sample 04. The metadata query is the article topic.

The article's `support-documents-v1` and `support-documents-embeddinggemma-v1` names are replaced with the fixed per-sample names above. The embedding profiles, model formatting, fixtures, package versions and topic APIs are preserved. Each Program.cs contains all its own types. No sample references another project or a shared code library.

Existing collections are validated for an unnamed dense vector, dimensions, cosine distance and declared profile. Validation fails if any of these checks do not match. Collections are never automatically deleted. These programs assume one setup process, as the articles do.

Stable point IDs and `wait: true` make repeated upserts inspectable. Repeat runs generate embeddings again. Azure calls incur usage charges. Upserts do not remove obsolete points or make multi-point updates atomic. Profile metadata does not prove model-artifact identity. Record the Ollama model digest when comparing results because the fixed tag is mutable. The article baseline retains Ollama's truncation default for this controlled short text.
