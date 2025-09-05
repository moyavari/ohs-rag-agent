using Qdrant.Client;
using Qdrant.Client.Grpc;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace OHS.Copilot.Infrastructure.VectorStores;

public class QdrantVectorStore : VectorStoreBase
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;
    private readonly int _vectorSize;

    public QdrantVectorStore(
        ILogger<QdrantVectorStore> logger,
        QdrantClient client,
        string collectionName = "ohs_chunks",
        int vectorSize = 1536) : base(logger)
    {
        _client = client;
        _collectionName = collectionName;
        _vectorSize = vectorSize;
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized) return;

        Logger.LogInformation("Initializing Qdrant Vector Store with collection {CollectionName}", _collectionName);

        try
        {
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            var collectionExists = collections.Any(c => c == _collectionName);

            if (!collectionExists)
            {
                Logger.LogInformation("Creating Qdrant collection {CollectionName}", _collectionName);
                
                await _client.CreateCollectionAsync(
                    collectionName: _collectionName,
                    vectorsConfig: new VectorParams
                    {
                        Size = (ulong)_vectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);
                
                Logger.LogInformation("Created Qdrant collection {CollectionName}", _collectionName);
            }

            IsInitialized = true;
            Logger.LogInformation("Qdrant Vector Store initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize Qdrant Vector Store");
            throw;
        }
    }

    public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            var collectionExists = collections.Any(c => c == _collectionName);
            return collectionExists;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Qdrant health check failed");
            return false;
        }
    }

    public override async Task UpsertAsync(Chunk chunk, float[] embedding, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var point = new PointStruct
            {
                Id = new PointId { Uuid = chunk.Id },
                Vectors = embedding,
                Payload =
                {
                    ["text"] = chunk.Text,
                    ["title"] = chunk.Title,
                    ["section"] = chunk.Section,
                    ["source_path"] = chunk.SourcePath,
                    ["hash"] = chunk.Hash,
                    ["created_at"] = chunk.CreatedAt.ToString("O")
                }
            };

            foreach (var metadata in chunk.Metadata)
            {
                point.Payload[metadata.Key] = metadata.Value?.ToString() ?? string.Empty;
            }

            await _client.UpsertAsync(
                collectionName: _collectionName,
                points: [point],
                cancellationToken: cancellationToken);

            Logger.LogDebug("Upserted point {ChunkId} to Qdrant collection {CollectionName}", chunk.Id, _collectionName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to upsert chunk {ChunkId} to Qdrant", chunk.Id);
            throw;
        }
    }

    public override async Task UpsertBatchAsync(IEnumerable<(Chunk chunk, float[] embedding)> items, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var points = items.Select(item =>
        {
            var point = new PointStruct
            {
                Id = new PointId { Uuid = item.chunk.Id },
                Vectors = item.embedding,
                Payload =
                {
                    ["text"] = item.chunk.Text,
                    ["title"] = item.chunk.Title,
                    ["section"] = item.chunk.Section,
                    ["source_path"] = item.chunk.SourcePath,
                    ["hash"] = item.chunk.Hash,
                    ["created_at"] = item.chunk.CreatedAt.ToString("O")
                }
            };

            foreach (var metadata in item.chunk.Metadata)
            {
                point.Payload[metadata.Key] = metadata.Value?.ToString() ?? string.Empty;
            }

            return point;
        }).ToList();

        try
        {
            await _client.UpsertAsync(
                collectionName: _collectionName,
                points: points,
                cancellationToken: cancellationToken);

            Logger.LogDebug("Batch upserted {Count} points to Qdrant collection {CollectionName}", points.Count, _collectionName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to batch upsert {Count} chunks to Qdrant", points.Count);
            throw;
        }
    }

    public override async Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK = 10, double minScore = 0.0, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var searchResult = await _client.SearchAsync(
                collectionName: _collectionName,
                vector: queryEmbedding,
                limit: (ulong)topK,
                scoreThreshold: (float)minScore,
                cancellationToken: cancellationToken);

            var results = searchResult.Select(point => 
            {
                var chunk = CreateChunkFromPayload(point.Id.ToString(), point.Payload);
                return VectorSearchResult.Create(point.Id.ToString(), point.Score, chunk);
            }).ToList();

            Logger.LogDebug("Search returned {Count} results from Qdrant", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to search Qdrant collection {CollectionName}", _collectionName);
            throw;
        }
    }

    public override async Task<Chunk?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var points = await _client.RetrieveAsync(
                collectionName: _collectionName,
                ids: [new PointId { Uuid = id }],
                withPayload: true,
                cancellationToken: cancellationToken);

            var point = points.FirstOrDefault();
            return point != null ? CreateChunkFromPayload(point.Id.ToString(), point.Payload) : null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get chunk {ChunkId} from Qdrant", id);
            throw;
        }
    }

    public override async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            await _client.DeleteAsync(
                collectionName: _collectionName,
                ids: [new PointId { Uuid = id }],
                cancellationToken: cancellationToken);

            Logger.LogDebug("Deleted point {Id} from Qdrant collection {CollectionName}", id, _collectionName);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete chunk {ChunkId} from Qdrant", id);
            return false;
        }
    }

    public override async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var info = await _client.GetCollectionInfoAsync(_collectionName, cancellationToken);
            return (long)info.PointsCount;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get count from Qdrant collection {CollectionName}", _collectionName);
            throw;
        }
    }

    private static Chunk CreateChunkFromPayload(string id, IDictionary<string, Value> payload)
    {
        var chunk = new Chunk
        {
            Id = id,
            Text = GetStringValue(payload, "text"),
            Title = GetStringValue(payload, "title"),
            Section = GetStringValue(payload, "section"),
            SourcePath = GetStringValue(payload, "source_path"),
            Hash = GetStringValue(payload, "hash")
        };

        if (DateTime.TryParse(GetStringValue(payload, "created_at"), out var createdAt))
        {
            chunk.CreatedAt = createdAt;
        }

        foreach (var kvp in payload)
        {
            if (!IsSystemField(kvp.Key))
            {
                chunk.Metadata[kvp.Key] = kvp.Value.StringValue;
            }
        }

        return chunk;
    }

    private static string GetStringValue(IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? value.StringValue : string.Empty;
    }

    private static bool IsSystemField(string fieldName)
    {
        return fieldName is "text" or "title" or "section" or "source_path" or "hash" or "created_at";
    }
}
