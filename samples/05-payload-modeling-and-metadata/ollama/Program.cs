using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

const string collectionName = "blog-samples-05-ollama-v1";
const string embeddingProfileId = "support-embeddinggemma-v1";
const int vectorSize = 768;

Func<IEmbeddingGenerator<string, Embedding<float>>> createGenerator = () =>
    new OllamaApiClient(
        new Uri(Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434"), "embeddinggemma:300m");

Func<string, string, string> prepareChunk = (title, text) =>
    $"title: {title} | text: {text}";
Func<string, string> prepareQuery = query =>
    $"task: search result | query: {query}";

const string chunkingVersion = "section-paragraphs-80w-overlap1-v1";
bool previewOnly = bool.Parse(Environment.GetEnvironmentVariable("PREVIEW_ONLY") ?? "true");

const string documentId = "worker-shutdown";
const string title = "Stop a background service cleanly";
SourceSection[] sections =
[
    new("shutdown", "Shutdown and the queue", [
        "When shutdown starts, stop accepting new work and complete " +
        "the channel writer. The reader can then drain the items " +
        "already in the queue.",
        "Drain the remaining items only while the shutdown deadline " +
        "allows it. If the deadline expires, cancel the active I/O " +
        "and leave unfinished work for the durable retry path.",
        "Pass the deadline cancellation token to each asynchronous " +
        "operation used during draining. A handler that ignores the " +
        "token can keep running after the shutdown budget expires."
    ]),
    new("capacity", "Queue capacity", [
        "Use a bounded Channel to limit queued work. With FullMode " +
        "set to Wait, producers await available capacity instead " +
        "of letting an in-memory backlog grow without a limit.",
        "Completing the writer prevents further writes. Producers " +
        "must handle channel completion during shutdown rather than " +
        "retrying against the same completed writer."
    ])
];

SourceDocument[] documents =
[
    new(documentId, title, "runbook", "published", sections),
    new("worker-shutdown-draft", title, "runbook", "draft", sections),
    new("queue-capacity-note", "Queue capacity", "note", "published",
        [sections.Single(section => section.SectionId == "capacity")])
];

var prepared = new List<(SourceDocument Document, List<SourceChunk> Chunks)>();
var documentIds = new HashSet<string>(StringComparer.Ordinal);

foreach (SourceDocument document in documents)
{
    document.Validate();
    if (!documentIds.Add(document.DocumentId))
    {
        throw new ArgumentException("Document IDs must be unique.");
    }

    List<SourceChunk> chunks = Chunker.Build(
        document.DocumentId, document.Sections, chunkingVersion,
        maxWords: 80, overlap: true);
    prepared.Add((document, chunks));

    foreach (SourceChunk chunk in chunks)
    {
        Console.WriteLine(
            $"{document.DocumentId}/{chunk.ChunkId} " +
            $"[{document.DocumentType}, {document.PublicationState}]");
        Console.WriteLine(chunk.Text);
        Console.WriteLine();
    }
}

if (previewOnly)
{
    return;
}

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
CancellationToken token = timeout.Token;
using var client = new QdrantClient(
    host: Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "127.0.0.1",
    port: int.Parse(Environment.GetEnvironmentVariable("QDRANT_GRPC_PORT") ?? "6334"));

// Preparation: this sample owns its collection; never delete an existing one.
if (!await client.CollectionExistsAsync(collectionName, token))
{
    await client.CreateCollectionAsync(
        collectionName: collectionName,
        vectorsConfig: new VectorParams { Size = (ulong)vectorSize, Distance = Distance.Cosine },
        metadata: new() { ["embedding-profile-id"] = embeddingProfileId },
        cancellationToken: token);
}

CollectionInfo collection = await client.GetCollectionInfoAsync(
    collectionName, token);
VectorsConfig? config = collection.Config.Params.VectorsConfig;
if (config is null ||
    config.ConfigCase != VectorsConfig.ConfigOneofCase.Params ||
    config.Params.Size != (ulong)vectorSize ||
    config.Params.Distance != Distance.Cosine ||
    !collection.Config.Metadata.TryGetValue(
        "embedding-profile-id", out Value? profile) ||
    profile.StringValue != embeddingProfileId)
{
    throw new InvalidOperationException("Collection contract mismatch.");
}

// Preparation: create or validate keyword indexes before ingestion.
string[] filterFields =
[
    "document_id", "chunking_version", "document_type", "publication_state"
];

foreach (string field in filterFields)
{
    if (collection.PayloadSchema.TryGetValue(field, out PayloadSchemaInfo? schema))
    {
        if (schema.DataType != PayloadSchemaType.Keyword)
        {
            throw new InvalidOperationException(
                $"Payload index '{field}' must use Keyword.");
        }

        continue;
    }

    await client.CreatePayloadIndexAsync(
        collectionName: collectionName,
        fieldName: field,
        schemaType: PayloadSchemaType.Keyword,
        wait: true,
        cancellationToken: token);
}

using IEmbeddingGenerator<string, Embedding<float>> generator = createGenerator();

foreach (var (document, chunks) in prepared)
{
    foreach (SourceChunk chunk in chunks)
    {
        ReadOnlyMemory<float> vector = await generator.GenerateVectorAsync(
            prepareChunk(document.Title, chunk.Text), cancellationToken: token);
        if (vector.Length != vectorSize)
        {
            throw new InvalidOperationException("Embedding dimension mismatch.");
        }

        await client.UpsertAsync(
            collectionName: collectionName,
            points: [new PointStruct
            {
                Id = chunk.PointId,
                Vectors = vector.ToArray(),
                Payload =
                {
                    ["document_id"] = document.DocumentId,
                    ["chunk_id"] = chunk.ChunkId,
                    ["chunking_version"] = chunkingVersion,
                    ["section_id"] = chunk.SectionId,
                    ["title"] = document.Title,
                    ["heading"] = chunk.Heading,
                    ["text"] = chunk.Text,
                    ["document_type"] = document.DocumentType,
                    ["publication_state"] = document.PublicationState
                }
            }],
            wait: true,
            cancellationToken: token);

        var stored = await client.RetrieveAsync(
            collectionName: collectionName,
            id: chunk.PointId,
            withPayload: true,
            withVectors: false,
            cancellationToken: token);
        if (stored.Count != 1 ||
            PayloadFields.RequiredString(stored[0].Payload, "document_id")
                != document.DocumentId ||
            PayloadFields.RequiredString(stored[0].Payload, "chunking_version")
                != chunkingVersion ||
            PayloadFields.RequiredString(stored[0].Payload, "text") != chunk.Text ||
            PayloadFields.RequiredString(stored[0].Payload, "document_type")
                != document.DocumentType ||
            PayloadFields.RequiredString(stored[0].Payload, "publication_state")
                != document.PublicationState)
        {
            throw new InvalidOperationException(
                $"Read-back failed: {document.DocumentId}/{chunk.ChunkId}");
        }
    }
}

// Article topic: restrict retrieval to the required source metadata.
const string question = "Should I drain all queued work during shutdown?";
ReadOnlyMemory<float> queryVector = await generator.GenerateVectorAsync(
    prepareQuery(question), cancellationToken: token);
if (queryVector.Length != vectorSize)
{
    throw new InvalidOperationException("Query dimension mismatch.");
}

string requiredDocumentType = Environment.GetEnvironmentVariable("REQUIRED_DOCUMENT_TYPE") ?? "runbook";
string requiredPublicationState = Environment.GetEnvironmentVariable("REQUIRED_PUBLICATION_STATE") ?? "published";

var eligibility = new Filter
{
    Must =
    {
        MatchKeyword("chunking_version", chunkingVersion),
        MatchKeyword("document_type", requiredDocumentType),
        MatchKeyword("publication_state", requiredPublicationState)
    }
};

string? requiredDocumentId = Environment.GetEnvironmentVariable("REQUIRED_DOCUMENT_ID");
if (!string.IsNullOrWhiteSpace(requiredDocumentId))
{
    eligibility.Must.Add(MatchKeyword("document_id", requiredDocumentId));
}

var results = await client.QueryAsync(
    collectionName: collectionName,
    query: queryVector.ToArray(),
    filter: eligibility,
    limit: 3,
    payloadSelector: new[]
    {
        "document_id", "chunk_id", "chunking_version",
        "document_type", "publication_state", "heading", "text"
    },
    vectorsSelector: false,
    cancellationToken: token);

Console.WriteLine($"Question: {question}");
if (results.Count == 0)
{
    Console.WriteLine("No chunks matched the required retrieval conditions.");
}

foreach (ScoredPoint result in results)
{
    string source = PayloadFields.RequiredString(result.Payload, "document_id");
    string chunk = PayloadFields.RequiredString(result.Payload, "chunk_id");
    string version = PayloadFields.RequiredString(result.Payload, "chunking_version");
    string type = PayloadFields.RequiredString(result.Payload, "document_type");
    string state = PayloadFields.RequiredString(result.Payload, "publication_state");
    if (version != chunkingVersion ||
        type != requiredDocumentType || state != requiredPublicationState)
    {
        throw new InvalidOperationException("Unexpected retrieval eligibility.");
    }

    Console.WriteLine($"{source}/{chunk} [{type}, {state}]: {result.Score:F4}");
    Console.WriteLine(PayloadFields.RequiredString(result.Payload, "heading"));
    Console.WriteLine(PayloadFields.RequiredString(result.Payload, "text"));
    Console.WriteLine();
}

public sealed record SourceSection(
    string SectionId, string Heading, string[] Paragraphs);

public sealed record SourceChunk(
    Guid PointId, string ChunkId, string SectionId,
    string Heading, string Text);

public static class Chunker
{
    public static int WordCount(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    public static List<SourceChunk> Build(
        string documentId, SourceSection[] sections, string version,
        int maxWords, bool overlap)
    {
        if (maxWords <= 0 || string.IsNullOrWhiteSpace(documentId) ||
            string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException(
                "Supply a document ID, chunking version, and positive word budget.");
        }

        var chunks = new List<SourceChunk>();
        var sectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SourceSection section in sections)
        {
            if (string.IsNullOrWhiteSpace(section.SectionId) ||
                !sectionIds.Add(section.SectionId) ||
                string.IsNullOrWhiteSpace(section.Heading) ||
                section.Paragraphs.Length == 0 ||
                section.Paragraphs.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Sections need unique IDs and nonempty text.");
            }

            int start = 0;
            while (start < section.Paragraphs.Length)
            {
                int end = start;
                int words = WordCount(section.Heading);
                while (end < section.Paragraphs.Length &&
                    words + WordCount(section.Paragraphs[end]) <= maxWords)
                {
                    words += WordCount(section.Paragraphs[end]);
                    end++;
                }

                if (end == start)
                {
                    throw new InvalidOperationException(
                        $"Paragraph {start} in '{section.SectionId}' exceeds the budget.");
                }

                string chunkId = $"{section.SectionId}/p{start}";
                string text = section.Heading + "\n\n" +
                    string.Join("\n\n", section.Paragraphs[start..end]);
                string identity = $"{version}\n{documentId}\n{chunkId}";
                byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));

                chunks.Add(new SourceChunk(
                    new Guid(hash.AsSpan(0, 16), bigEndian: true), chunkId,
                    section.SectionId, section.Heading, text));

                start = overlap && end < section.Paragraphs.Length && end - start > 1
                    ? end - 1
                    : end;
            }
        }

        return chunks;
    }
}

public sealed record SourceDocument(
    string DocumentId, string Title, string DocumentType,
    string PublicationState, SourceSection[] Sections)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DocumentId) ||
            string.IsNullOrWhiteSpace(Title) ||
            DocumentType is not ("runbook" or "note") ||
            PublicationState is not ("published" or "draft"))
        {
            throw new ArgumentException("Invalid source document metadata.");
        }
    }
}

public static class PayloadFields
{
    public static string RequiredString(
        IReadOnlyDictionary<string, Value> payload, string field)
    {
        if (!payload.TryGetValue(field, out Value? value) ||
            value.KindCase != Value.KindOneofCase.StringValue ||
            string.IsNullOrWhiteSpace(value.StringValue))
        {
            throw new InvalidOperationException(
                $"Payload field '{field}' must be a nonempty scalar string.");
        }

        return value.StringValue;
    }
}
