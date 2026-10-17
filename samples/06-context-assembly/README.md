# 06: Assemble context for the model

Article: [Assembling Context for the Model](https://www.lukaswalter.dev/posts/assembling-context-for-the-model/).

Assemble eligible Qdrant results into a token-counted model request. Remove exact repeated paragraphs within a source scope, retain the text actually admitted, and resolve citation IDs back to that evidence.

## Run from the repository root

See the [root setup](../../README.md) for prerequisites and embedding-provider configuration. The default preview prints the seven source chunks and their metadata without contacting Qdrant or an embedding provider.

```bash
dotnet run --project samples/06-context-assembly/azure-openai/Sample.csproj
dotnet run --project samples/06-context-assembly/ollama/Sample.csproj
```

For retrieval and context assembly, start Qdrant with `docker compose up -d`, configure the chosen embedding provider, and run:

```bash
PREVIEW_ONLY=false dotnet run --project samples/06-context-assembly/azure-openai/Sample.csproj
PREVIEW_ONLY=false dotnet run --project samples/06-context-assembly/ollama/Sample.csproj
```

These commands stop after printing the assembled request. Answer generation is optional.

## Configuration

| Setting | Azure OpenAI | Ollama |
| --- | --- | --- |
| Collection (fixed, isolated) | `blog-samples-06-azure-openai-v1` | `blog-samples-06-ollama-v1` |
| Embedding profile | `support-text-v1` | `support-embeddinggemma-v1` |
| Embedding model | `text-embedding-3-small` | `embeddinggemma:300m` |
| Vector | 1,536-dimensional cosine | 768-dimensional cosine |

`QDRANT_HOST` defaults to `127.0.0.1`, and `QDRANT_GRPC_PORT` defaults to `6334`.

For retrieval, Azure needs `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` and `AZURE_OPENAI_API_KEY`. Ollama uses `OLLAMA_ENDPOINT` (default `http://localhost:11434`) and requires `embeddinggemma:300m`. The embedding profiles and input formatting match the earlier samples.

| Variable | Default | Effect |
| --- | --- | --- |
| `PREVIEW_ONLY` | `true` | Print the source chunks and exit before opening clients |
| `RUN_BUDGET_CHECKS` | `false` | Run the article's offline budget assertions and exit before preview or retrieval |
| `INPUT_TEXT_ALLOWANCE` | `3328` | Maximum combined token count of instructions and the serialized user message |
| `GENERATE_ANSWER` | `false` | Call Azure OpenAI after retrieval and assembly if evidence was admitted |
| `REQUIRED_DOCUMENT_TYPE` | `runbook` | Required document type in the Qdrant query |
| `REQUIRED_PUBLICATION_STATE` | `published` | Required publication state in the Qdrant query |
| `REQUIRED_DOCUMENT_ID` | Unset | Optional additional document scope |

Boolean settings accept `true` or `false`. `INPUT_TEXT_ALLOWANCE` must be an integer large enough for the instructions and serialized question. An allowance below that minimum fails before generation.

The default allowance comes from the article's application-defined budget of 4,096 tokens. Reserving 512 for output and 256 for message framing and counting differences leaves a local text-token allowance of 3,328. This is an estimate, not a guarantee that the provider will count the complete request within that budget.

The sample estimates input text tokens with `o200k_base`. Select the tokenizer based on the generation model, which can differ from the embedding model. The vocabulary is loaded locally. Both projects pin Microsoft.ML.Tokenizers and Microsoft.ML.Tokenizers.Data.O200kBase 2.0.0 and explicitly reference Microsoft.Bcl.Memory 9.0.14, matching the article's package baseline.

## Check the budget without services

```bash
RUN_BUDGET_CHECKS=true dotnet run --project samples/06-context-assembly/azure-openai/Sample.csproj
RUN_BUDGET_CHECKS=true dotnet run --project samples/06-context-assembly/ollama/Sample.csproj
```

Expect `Budget checks passed.` Both variants verify that a larger, higher-ranked candidate is rejected without suppressing a later candidate's paragraphs, that a request exactly at the measured allowance fits, and that the same request fails admission one token below that allowance. These checks need neither Qdrant nor Azure or Ollama configuration.

## Optional answer generation

Both embedding variants use Azure OpenAI for the article's optional generation step. The Ollama variant keeps embeddings local, but answer generation needs a separate Azure setup.

Configure `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY` and `AZURE_OPENAI_CHAT_DEPLOYMENT`. Select a chat deployment compatible with the sample's `o200k_base` tokenizer. Azure deployment names do not necessarily identify the underlying model or tokenizer.

Using Ollama or another provider for generation requires adapting the generation call and selecting the appropriate tokenizer.

```bash
export AZURE_OPENAI_CHAT_DEPLOYMENT="your-chat-deployment"

PREVIEW_ONLY=false GENERATE_ANSWER=true dotnet run --project samples/06-context-assembly/azure-openai/Sample.csproj
PREVIEW_ONLY=false GENERATE_ANSWER=true dotnet run --project samples/06-context-assembly/ollama/Sample.csproj
```

The sample requests a JSON answer with `status`, `answer` and `sourceIds`. It accepts a consistent `insufficient` response or a nonempty `answered` response whose source IDs all refer to admitted evidence. Malformed JSON, incompatible field types, inconsistent status or unknown IDs fail validation. Known citation IDs do not prove that an answer's claims are supported.

Generation shares the article's two-minute deadline with embedding and Qdrant operations. The program prints the local input-text token count and provider-reported input usage when available. Local counting and the framing reserve do not guarantee the provider's full request size.

## Expected result

Preview prints seven chunks: three from the published runbook, three from its draft copy and one published note.

Retrieval mode seeds those seven points, validates their payloads and queries the published runbook. After `Question:`, inspect the assembly decisions, `Input text tokens: .../3328`, and the serialized JSON evidence and question. With the default fixtures and allowance, all three eligible chunks fit. The deadline paragraph appears once in the assembled evidence, and the unique shutdown and cancellation paragraphs survive. Which source retains the deadline depends on retrieval order.

Request-local labels such as `S1` map to the original document, chunk and chunking version through `SelectedSource`. `IncludedText` is the text sent to the model. `Original.Text` still contains the full retrieved chunk.

Repeated runs against a collection containing only these fixtures keep seven points with stable IDs. Each retrieval run generates embeddings again. Generation is disabled unless explicitly enabled.

To inspect an empty eligible result without relaxing the published-runbook conditions:

```bash
PREVIEW_ONLY=false REQUIRED_DOCUMENT_ID=worker-shutdown-draft dotnet run --project samples/06-context-assembly/ollama/Sample.csproj
```

Expect `No evidence was admitted; skip generation.` Azure supports the same metadata variables. Setting `REQUIRED_PUBLICATION_STATE=draft` selects the draft runbook, and `REQUIRED_DOCUMENT_TYPE=note` selects the published note. Assembly only receives results that passed the required metadata checks.

## Preparation and article differences

This program includes the complete chunker, metadata fixtures, collection validation, four keyword indexes and ingestion/read-back loop. You do not need to run sample 05. Preparation is followed by the article's candidate adapter, context assembler and optional generation/citation checks. Every helper is in this Program.cs, with no project dependencies between samples.

The article's shared collection names are replaced with the isolated sample 06 names above. Existing collections are validated and never automatically deleted. Stable upserts do not remove obsolete points.

The article's budget assertions run only when `RUN_BUDGET_CHECKS=true`. Its constants for preview, generation, input allowance and metadata cases are exposed through the environment variables above. Package versions, embedding profiles, fixtures, assembly policy and citation contract follow the article. Both projects include the Azure chat packages so either embedding path can exercise optional generation.

Exact-text deduplication is scoped by document, section and chunking version. It does not compare source positions or preserve every procedural relationship across passages. Token-budget admission and citation-ID validation do not establish that the evidence is sufficient or the generated answer is grounded.
