using Qdrant.Client;
using Qdrant.Client.Grpc;

const string collectionName = "blog-samples-01-azure-openai-v1";
const string embeddingProfileId = "support-text-v1";
const ulong vectorSize = 1536;

using var executionTimeout =
    new CancellationTokenSource(TimeSpan.FromSeconds(10));
using var client = new QdrantClient(
    host: Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "127.0.0.1",
    port: int.Parse(Environment.GetEnvironmentVariable("QDRANT_GRPC_PORT") ?? "6334"));

if (!await client.CollectionExistsAsync(collectionName, executionTimeout.Token))
{
    await client.CreateCollectionAsync(
        collectionName: collectionName,
        vectorsConfig: new VectorParams
        {
            Size = vectorSize,
            Distance = Distance.Cosine
        },
        metadata: new()
        {
            ["embedding-profile-id"] = embeddingProfileId
        },
        cancellationToken: executionTimeout.Token);
}

CollectionInfo collection = await client.GetCollectionInfoAsync(
    collectionName,
    executionTimeout.Token);

VectorsConfig? vectorsConfig = collection.Config.Params.VectorsConfig;

if (vectorsConfig is null ||
    vectorsConfig.ConfigCase != VectorsConfig.ConfigOneofCase.Params)
{
    throw new InvalidOperationException(
        $"Collection '{collectionName}' does not use an unnamed " +
        "dense-vector configuration.");
}

VectorParams vectors = vectorsConfig.Params;

if (vectors.Size != vectorSize || vectors.Distance != Distance.Cosine)
{
    throw new InvalidOperationException(
        $"Collection '{collectionName}' does not match the expected " +
        $"{vectorSize}-dimension cosine configuration.");
}

if (!collection.Config.Metadata.TryGetValue(
        "embedding-profile-id",
        out Value? storedProfile) ||
    storedProfile.StringValue != embeddingProfileId)
{
    throw new InvalidOperationException(
        $"Collection '{collectionName}' does not declare the expected " +
        $"embedding profile '{embeddingProfileId}'.");
}

Console.WriteLine(
    $"Collection '{collectionName}' is {collection.Status} with " +
    $"{vectors.Size} dimensions, {vectors.Distance} distance, and " +
    $"embedding profile '{embeddingProfileId}'.");
