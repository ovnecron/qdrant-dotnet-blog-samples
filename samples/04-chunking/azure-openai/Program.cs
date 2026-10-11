using System.Security.Cryptography;
using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.ClientModel;
using static Qdrant.Client.Grpc.Conditions;

const string collectionName = "blog-samples-04-azure-openai-v1";
const string embeddingProfileId = "support-text-v1";
const int vectorSize = 1536;

Func<IEmbeddingGenerator<string, Embedding<float>>> createGenerator = () =>
{
    string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
        ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT.");
    string deployment = Environment.GetEnvironmentVariable(
        "AZURE_OPENAI_EMBEDDING_DEPLOYMENT")
        ?? throw new InvalidOperationException("Set AZURE_OPENAI_EMBEDDING_DEPLOYMENT.");
    string key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
        ?? throw new InvalidOperationException("Set AZURE_OPENAI_API_KEY.");

    return new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key))
        .GetEmbeddingClient(deployment)
        .AsIEmbeddingGenerator();
};

Func<string, string, string> prepareChunk = (title, text) => text;
Func<string, string> prepareQuery = query => query;

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

List<SourceChunk> chunks = Chunker.Build(
    documentId, sections, chunkingVersion, maxWords: 80, overlap: true);

foreach (SourceChunk chunk in chunks)
{
    Console.WriteLine($"{chunk.ChunkId} ({Chunker.WordCount(chunk.Text)} words)");
    Console.WriteLine(chunk.Text);
    Console.WriteLine();
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

using IEmbeddingGenerator<string, Embedding<float>> generator = createGenerator();

foreach (SourceChunk chunk in chunks)
{
    ReadOnlyMemory<float> vector = await generator.GenerateVectorAsync(
        prepareChunk(title, chunk.Text), cancellationToken: token);
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
                ["document_id"] = documentId,
                ["chunk_id"] = chunk.ChunkId,
                ["chunking_version"] = chunkingVersion,
                ["section_id"] = chunk.SectionId,
                ["title"] = title,
                ["heading"] = chunk.Heading,
                ["text"] = chunk.Text
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
        !stored[0].Payload.TryGetValue("text", out Value? storedText) ||
        storedText.StringValue != chunk.Text)
    {
        throw new InvalidOperationException($"Read-back failed: {chunk.ChunkId}");
    }
}

const string question = "Should I drain all queued work during shutdown?";
ReadOnlyMemory<float> queryVector = await generator.GenerateVectorAsync(
    prepareQuery(question), cancellationToken: token);
if (queryVector.Length != vectorSize)
{
    throw new InvalidOperationException("Query dimension mismatch.");
}

var results = await client.QueryAsync(
    collectionName: collectionName,
    query: queryVector.ToArray(),
    filter: new Filter
    {
        Must =
        {
            Match("document_id", new[] { documentId }),
            Match("chunking_version", new[] { chunkingVersion })
        }
    },
    limit: 3,
    payloadSelector: new[] { "chunk_id", "heading", "text" },
    vectorsSelector: false,
    cancellationToken: token);

Console.WriteLine($"Question: {question}");
foreach (ScoredPoint result in results)
{
    Console.WriteLine($"{result.Payload["chunk_id"].StringValue}: {result.Score:F4}");
    Console.WriteLine(result.Payload["text"].StringValue);
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
