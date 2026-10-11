using Microsoft.Extensions.AI;
using OllamaSharp;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

const string collectionName = "blog-samples-03-ollama-v1";
const string embeddingProfileId = "support-embeddinggemma-v1";
const int vectorSize = 768;

using IEmbeddingGenerator<string, Embedding<float>> generator =
    new OllamaApiClient(
        new Uri(Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434"),
        "embeddinggemma:300m");

Func<string, string> prepareQuery = query =>
    $"task: search result | query: {query}";

Func<SourceDocument, string> prepareDocument = document => $"title: {document.Title} | text: {document.Text}";

using var executionTimeout =
    new CancellationTokenSource(TimeSpan.FromMinutes(2));
CancellationToken token = executionTimeout.Token;

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
    config.Params.Distance != Distance.Cosine)
{
    throw new InvalidOperationException(
        $"Collection '{collectionName}' does not use the expected " +
        $"unnamed {vectorSize}-dimension cosine vector.");
}

if (!collection.Config.Metadata.TryGetValue(
        "embedding-profile-id", out Value? storedProfile) ||
    storedProfile.StringValue != embeddingProfileId)
{
    throw new InvalidOperationException(
        $"Collection '{collectionName}' does not match profile '{embeddingProfileId}'.");
}

// Preparation: seed this independent sample using the article's three notes.
SourceDocument[] documents =
[
    new(
        Guid.Parse("19e8e940-0562-4b6b-bca5-1f99ea154f2e"),
        "worker-shutdown",
        "Stop a background service cleanly",
        "Pass the stopping token to asynchronous I/O in a BackgroundService. " +
        "When shutdown starts, let the operation observe cancellation " +
        "and finish within the host shutdown timeout."),
    new(
        Guid.Parse("81852b6d-d848-4a62-a777-e45b75f56755"),
        "http-client-lifetime",
        "Create HTTP clients through the factory",
        "Use IHttpClientFactory to create named HTTP clients. " +
        "The factory manages handler lifetimes so callers can dispose " +
        "their clients without creating a new connection pool each time."),
    new(
        Guid.Parse("fcad478c-cdae-428e-8784-c07d7ec9b8c3"),
        "bounded-work-queue",
        "Limit queued background work",
        "Use a bounded Channel to limit queued work. With FullMode set " +
        "to Wait, producers await available capacity instead of letting " +
        "an in-memory backlog grow without a limit.")
];

foreach (SourceDocument document in documents)
{
    if (string.IsNullOrWhiteSpace(document.Text))
    {
        throw new InvalidOperationException(
            $"Document '{document.DocumentId}' has no text.");
    }

    ReadOnlyMemory<float> vector = await generator.GenerateVectorAsync(
        prepareDocument(document), cancellationToken: token);

    if (vector.Length != vectorSize)
    {
        throw new InvalidOperationException(
            $"Expected {vectorSize} values, received {vector.Length}.");
    }

    var point = new PointStruct
    {
        Id = document.PointId,
        Vectors = vector.ToArray(),
        Payload =
        {
            ["document_id"] = document.DocumentId,
            ["chunk_id"] = "whole-document",
            ["title"] = document.Title,
            ["text"] = document.Text
        }
    };

    await client.UpsertAsync(
        collectionName: collectionName,
        points: [point],
        wait: true,
        cancellationToken: token);

    var stored = await client.RetrieveAsync(
        collectionName: collectionName,
        id: document.PointId,
        withPayload: true,
        withVectors: false,
        cancellationToken: token);

    if (stored.Count != 1 ||
        !stored[0].Payload.TryGetValue("text", out Value? storedText) ||
        storedText.StringValue != document.Text ||
        !stored[0].Payload.TryGetValue("title", out Value? storedTitle))
    {
        throw new InvalidOperationException(
            $"Read-back failed for '{document.DocumentId}'.");
    }

    Console.WriteLine($"Indexed '{document.DocumentId}' into '{collectionName}'.");
    Console.WriteLine($"Point ID: {document.PointId}");
    Console.WriteLine($"Title: {storedTitle.StringValue}");
    Console.WriteLine($"Text: {storedText.StringValue}");
}


// Article topic: embed the question and filter during retrieval.
const string question =
    "How do I stop a background service without abandoning I/O?";

ReadOnlyMemory<float> queryVector = await generator.GenerateVectorAsync(
    prepareQuery(question), cancellationToken: token);

if (queryVector.Length != vectorSize)
{
    throw new InvalidOperationException(
        $"Expected {vectorSize} values, received {queryVector.Length}.");
}

string[] eligibleDocumentIds =
    ["worker-shutdown", "bounded-work-queue"];
Filter eligibilityFilter = Match("document_id", eligibleDocumentIds);

IReadOnlyList<ScoredPoint> results = await client.QueryAsync(
    collectionName: collectionName,
    query: queryVector.ToArray(),
    filter: eligibilityFilter,
    limit: 3,
    payloadSelector: new[] { "document_id", "title", "text" },
    vectorsSelector: false,
    cancellationToken: token);

Console.WriteLine($"Question: {question}");

foreach (ScoredPoint result in results)
{
    if (!result.Payload.TryGetValue(
            "document_id", out Value? documentId) ||
        !result.Payload.TryGetValue("title", out Value? title) ||
        !result.Payload.TryGetValue("text", out Value? text))
    {
        throw new InvalidOperationException(
            $"Point '{result.Id}' is missing the expected payload.");
    }

    Console.WriteLine();
    Console.WriteLine($"Score: {result.Score:F4}");
    Console.WriteLine($"Document: {documentId.StringValue}");
    Console.WriteLine($"Title: {title.StringValue}");
    Console.WriteLine($"Text: {text.StringValue}");
}

public sealed record SourceDocument(
    Guid PointId,
    string DocumentId,
    string Title,
    string Text);
