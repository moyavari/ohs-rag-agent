using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.Entities;
using System.Text.Json.Serialization;

namespace OHS.Copilot.Infrastructure.VectorStores;

public class CosmosVectorStore : VectorStoreBase
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseId;
    private readonly string _containerId;
    private Database? _database;
    private Container? _container;

    public CosmosVectorStore(
        ILogger<CosmosVectorStore> logger,
        CosmosClient cosmosClient,
        string databaseId = "OHSCopilot",
        string containerId = "chunks") : base(logger)
    {
        _cosmosClient = cosmosClient;
        _databaseId = databaseId;
        _containerId = containerId;
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized) return;

        try
        {
            _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId, cancellationToken: cancellationToken);

            var containerProperties = new ContainerProperties(_containerId, "/id");

            var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                containerProperties,
                throughput: 1000);
                
            _container = containerResponse.Container;

            IsInitialized = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize Cosmos DB Vector Store");
            throw;
        }
    }

    public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_container == null) return false;
            
            await _container.ReadContainerAsync();
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Cosmos DB health check failed");
            return false;
        }
    }

    public override async Task UpsertAsync(Chunk chunk, float[] embedding, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        if (_container == null)
            throw new InvalidOperationException("Container not initialized");

        try
        {
            var cosmosDocument = new CosmosChunkDocument
            {
                id = chunk.Id,
                text = chunk.Text,
                title = chunk.Title,
                section = chunk.Section,
                sourcePath = chunk.SourcePath,
                hash = chunk.Hash,
                embedding = embedding,
                createdAt = chunk.CreatedAt.ToString("O"),
                updatedAt = DateTime.UtcNow.ToString("O"),
                metadata = chunk.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty)
            };

            await _container.UpsertItemAsync(cosmosDocument, new PartitionKey(cosmosDocument.id), cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to upsert chunk {ChunkId} to Cosmos DB", chunk.Id);
            throw;
        }
    }

    public override async Task UpsertBatchAsync(IEnumerable<(Chunk chunk, float[] embedding)> items, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        if (_container == null)
            throw new InvalidOperationException("Container not initialized");

        var itemList = items.ToList();
        
        try
        {
            var tasks = itemList.Select(async item =>
            {
                var cosmosDocument = new CosmosChunkDocument
                {
                    id = item.chunk.Id,
                    text = item.chunk.Text,
                    title = item.chunk.Title,
                    section = item.chunk.Section,
                    sourcePath = item.chunk.SourcePath,
                    hash = item.chunk.Hash,
                    embedding = item.embedding,
                    createdAt = item.chunk.CreatedAt.ToString("O"),
                    updatedAt = DateTime.UtcNow.ToString("O"),
                    metadata = item.chunk.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty)
                };

                var partitionKey = new PartitionKey(cosmosDocument.id);
                return await _container.UpsertItemAsync(cosmosDocument, partitionKey, cancellationToken: cancellationToken);
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to batch upsert {Count} chunks to Cosmos DB", itemList.Count);
            throw;
        }
    }

    public override async Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK = 10, double minScore = 0.0, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        if (_container == null)
            throw new InvalidOperationException("Container not initialized");

        try
        {
            
            var queryDefinition = new QueryDefinition("SELECT * FROM c");
            var results = new List<VectorSearchResult>();
            
            using var iterator = _container.GetItemQueryIterator<CosmosChunkDocument>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                
                foreach (var doc in response)
                {
                    if (doc.embedding == null || doc.embedding.Length == 0)
                        continue;
                    
                    var similarity = CalculateCosineSimilarity(queryEmbedding, doc.embedding);
                    
                    if (similarity >= minScore)
                    {
                        var chunk = new Chunk
                        {
                            Id = doc.id,
                            Text = doc.text,
                            Title = doc.title,
                            Section = doc.section,
                            SourcePath = doc.sourcePath,
                            Hash = doc.hash,
                            CreatedAt = DateTime.TryParse(doc.createdAt, out var createdAt) ? createdAt : DateTime.UtcNow,
                            Metadata = doc.metadata?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new Dictionary<string, object>()
                        };
                        
                        results.Add(VectorSearchResult.Create(doc.id, similarity, chunk));
                    }
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to search Cosmos DB vector store");
            throw;
        }
    }

    public override async Task<Chunk?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        if (_container == null)
            throw new InvalidOperationException("Container not initialized");

        try
        {
            var response = await _container.ReadItemAsync<CosmosChunkDocument>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            var document = response.Resource;
            
            return new Chunk
            {
                Id = document.id,
                Text = document.text,
                Title = document.title,
                Section = document.section,
                SourcePath = document.sourcePath,
                Hash = document.hash,
                CreatedAt = DateTime.TryParse(document.createdAt, out var createdAt) ? createdAt : DateTime.UtcNow,
                Metadata = document.metadata?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new Dictionary<string, object>()
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get chunk {ChunkId} from Cosmos DB", id);
            throw;
        }
    }

    public override async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        if (_container == null)
            throw new InvalidOperationException("Container not initialized");

        try
        {
            await _container.DeleteItemAsync<CosmosChunkDocument>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete chunk {ChunkId} from Cosmos DB", id);
            return false;
        }
    }

    public override async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        if (_container == null)
            throw new InvalidOperationException("Container not initialized");

        try
        {
            var queryDefinition = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
            
            using var iterator = _container.GetItemQueryIterator<long>(queryDefinition);
            var response = await iterator.ReadNextAsync(cancellationToken);
            
            return response.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get count from Cosmos DB vector store");
            throw;
        }
    }

    private class CosmosChunkDocument
    {
        public string id { get; set; } = string.Empty;
        public string text { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string section { get; set; } = string.Empty;
        public string sourcePath { get; set; } = string.Empty;
        public string hash { get; set; } = string.Empty;
        public float[]? embedding { get; set; }
        public string createdAt { get; set; } = string.Empty;
        public string updatedAt { get; set; } = string.Empty;
        public Dictionary<string, string>? metadata { get; set; }
    }

}
