# qdrant-dotnet-blog-samples

Runnable .NET samples for my Qdrant articles, with Azure OpenAI and local Ollama variants. The articles build on each other, but every sample in this repository runs independently. You can start with any sample without running earlier ones or copying code between projects. This repository follows the articles' code and fixtures. No API, AppHost or shared sample library is included.

## Prerequisites and pinned baseline

- .NET SDK **10.0.102**, selected by `global.json` with roll-forward disabled.
- Docker with Compose for local Qdrant **1.19.0** (HTTP 6333, gRPC 6334). On Windows, start Docker Desktop with Linux containers enabled.
- Azure OpenAI configuration or Ollama **0.11.10+** with `embeddinggemma:300m` for indexing and querying. Sample 01 and offline previews do not need an embedding provider.

Every project targets .NET 10 and pins Qdrant.Client 1.19.0. Azure embedding programs pin Microsoft.Extensions.AI.OpenAI 10.9.0 and Azure.AI.OpenAI 2.1.0. Ollama embedding programs pin Microsoft.Extensions.AI 10.9.0 and OllamaSharp 5.4.30. Sample 01 references only Qdrant.Client, regardless of provider folder.

OllamaSharp may emit CS9057 about its optional tool source generator with SDK 10.0.102. These programs do not use generated tools. The warning is documented rather than suppressed.

## Shells

Commands in `shell` blocks work in PowerShell and Bash/Zsh. Where environment-variable syntax differs, choose the block for your shell. The PowerShell examples use syntax available in Windows PowerShell 5.1 and PowerShell 7.

Run all commands from the repository root. Set Azure and Ollama connection variables in the same terminal you use to run the samples.

PowerShell's `$env:` assignments remain active in the current terminal. The run examples clear their temporary mode and filter settings in `finally` blocks, returning those settings to the program defaults afterward. Connection variables remain available for later commands in that terminal.

## Start Qdrant

```shell
docker compose up -d
docker compose ps
```

The pinned image stores data in the Compose project's named volume. Ports are bound to 127.0.0.1. Open the [dashboard](http://127.0.0.1:6333/dashboard). Wait until Qdrant is ready before running programs. If a compatible local Qdrant already occupies these ports, use that server instead of starting a second container.

```shell
docker compose stop
docker compose start
```

Stopping preserves data. No sample deletes or recreates a collection. The local server has no authentication or TLS. This setup is intended for local development.

## Azure OpenAI

Deploy **text-embedding-3-small** with the default **1,536 dimensions**. In the terminal that runs the sample:

**Bash/Zsh**

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_EMBEDDING_DEPLOYMENT="support-embeddings"
# Enter the key at this prompt. Input is hidden and stays out of shell history.
read -r -s AZURE_OPENAI_API_KEY
export AZURE_OPENAI_API_KEY
```

**PowerShell**

```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT = "support-embeddings"
$azureOpenAiApiKey = Read-Host "Azure OpenAI API key" -AsSecureString
$env:AZURE_OPENAI_API_KEY = [System.Net.NetworkCredential]::new("", $azureOpenAiApiKey).Password
Remove-Variable azureOpenAiApiKey
```

Both key prompts hide the input. Use the resource endpoint, not a Foundry project URL or an endpoint ending in `/openai/v1`. The deployment variable is the deployment name, which can differ from the model name. Requests send the sample text to Azure and incur usage charges. Programs read environment variables directly. They do not automatically load `.env` files.

The `support-text-v1` contract is text-embedding-3-small, 1,536-dimensional cosine vectors and plain-text-v1 preprocessing. Titles remain in payload and are not prefixed to Azure embedding inputs.

## Ollama

Start the Ollama desktop app, or run `ollama serve` in another terminal. Then:

```shell
ollama pull embeddinggemma:300m
ollama list
```

Set the endpoint in the terminal that runs the sample:

**Bash/Zsh**

```bash
export OLLAMA_ENDPOINT="http://localhost:11434"
```

**PowerShell**

```powershell
$env:OLLAMA_ENDPOINT = "http://localhost:11434"
```

The `support-embeddinggemma-v1` contract is EmbeddingGemma 300m, full 768-dimensional cosine vectors, documents formatted as `title: ... | text: ...`, and queries as `task: search result | query: ...`. Record the model digest from `ollama list` when reproducing results: the tag is mutable and profile-ID validation does not check artifact identity. The article baseline keeps the default truncation behavior for the short fixtures. It does not implement token-limit enforcement.

## Samples

| Sample | Learning goal and local instructions | Direct article |
| --- | --- | --- |
| 01 | [Create and validate a collection](samples/01-first-collection/README.md) | [Article](https://www.lukaswalter.dev/posts/your-first-qdrant-collection/) |
| 02 | [Index and read back documents](samples/02-indexing-documents/README.md) | [Article](https://www.lukaswalter.dev/posts/indexing-your-first-documents/) |
| 03 | [Query with a document allowlist](samples/03-querying/README.md) | [Article](https://www.lukaswalter.dev/posts/querying-qdrant-from-dotnet/) |
| 04 | [Chunk along section boundaries](samples/04-chunking/README.md) | [Article](https://www.lukaswalter.dev/posts/chunking-before-it-becomes-a-quality-problem/) |
| 05 | [Model payload and filter by metadata](samples/05-payload-modeling-and-metadata/README.md) | [Article](https://www.lukaswalter.dev/posts/payload-modeling-and-metadata-filters/) |
| 06 | [Assemble context for the model](samples/06-context-assembly/README.md) | [Article](https://www.lukaswalter.dev/posts/assembling-context-for-the-model/) |

Each sample is a standalone program: run it directly without running earlier samples or copying code between projects. Index/query programs create or validate their collection and seed their own data before demonstrating the article topic.

Each sample owns `blog-samples-NN-azure-openai-v1` and `blog-samples-NN-ollama-v1`, replacing the article's shared collection names. `NN` is the two-digit sample number. For sample 02, the names are `blog-samples-02-azure-openai-v1` and `blog-samples-02-ollama-v1`. The `v1` suffix versions the collection contract. Separate collections keep experiments in one sample from affecting another and keep the providers' incompatible embedding spaces apart.

Collection validation checks the unnamed dense-vector variant, size, distance and embedding-profile ID. It intentionally retains the articles' narrow contract checks. Stable IDs prevent duplicate sample points, but do not reconcile deleted input or make ingestion atomic.

Sample 01 needs only Qdrant, even in the Azure folder. Samples 04–06 default to offline previews of chunks and metadata. These previews need neither Qdrant nor an embedding provider. Sample 06 also provides offline budget checks and optional Azure answer generation, described in its README.

## Restore, build and run

All commands below run from the repository root. Building needs no provider credentials:

```shell
dotnet restore QdrantSamples.slnx --locked-mode
dotnet build QdrantSamples.slnx --no-restore --configuration Release
dotnet run --project samples/01-first-collection/azure-openai/Sample.csproj
dotnet run --project samples/01-first-collection/ollama/Sample.csproj
dotnet run --project samples/02-indexing-documents/azure-openai/Sample.csproj
dotnet run --project samples/02-indexing-documents/ollama/Sample.csproj
dotnet run --project samples/03-querying/azure-openai/Sample.csproj
dotnet run --project samples/03-querying/ollama/Sample.csproj
dotnet run --project samples/04-chunking/azure-openai/Sample.csproj
dotnet run --project samples/04-chunking/ollama/Sample.csproj
dotnet run --project samples/05-payload-modeling-and-metadata/azure-openai/Sample.csproj
dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
dotnet run --project samples/06-context-assembly/azure-openai/Sample.csproj
dotnet run --project samples/06-context-assembly/ollama/Sample.csproj
```

For indexing, querying and context assembly where applicable:

**Bash/Zsh**

```bash
PREVIEW_ONLY=false dotnet run --project samples/04-chunking/azure-openai/Sample.csproj
PREVIEW_ONLY=false dotnet run --project samples/04-chunking/ollama/Sample.csproj
PREVIEW_ONLY=false dotnet run --project samples/05-payload-modeling-and-metadata/azure-openai/Sample.csproj
PREVIEW_ONLY=false dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
PREVIEW_ONLY=false dotnet run --project samples/06-context-assembly/azure-openai/Sample.csproj
PREVIEW_ONLY=false dotnet run --project samples/06-context-assembly/ollama/Sample.csproj
```

**PowerShell**

```powershell
$env:PREVIEW_ONLY = "false"
try {
    dotnet run --project samples/04-chunking/azure-openai/Sample.csproj
    dotnet run --project samples/04-chunking/ollama/Sample.csproj
    dotnet run --project samples/05-payload-modeling-and-metadata/azure-openai/Sample.csproj
    dotnet run --project samples/05-payload-modeling-and-metadata/ollama/Sample.csproj
    dotnet run --project samples/06-context-assembly/azure-openai/Sample.csproj
    dotnet run --project samples/06-context-assembly/ollama/Sample.csproj
}
finally {
    Remove-Item Env:PREVIEW_ONLY -ErrorAction SilentlyContinue
}
```

`QDRANT_HOST` defaults to `127.0.0.1` and `QDRANT_GRPC_PORT` to `6334`. The programs use local plaintext gRPC. Timeout bounds remain the article defaults: 10 seconds for sample 01, two minutes for index/query programs. Samples 02 and 03 make four or fewer embedding calls per run. The sample READMEs document the expected results.
