using System.Text.Json;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace OHS.Copilot.Infrastructure.VectorStores;

public class JsonVectorStore : VectorStoreBase
{
    private readonly string _dataPath;
    private readonly Dictionary<string, VectorEntry> _vectors = [];
    private readonly object _lock = new();

    public JsonVectorStore(ILogger<JsonVectorStore> logger, string dataPath = "./data/vectors.json") 
        : base(logger)
    {
        _dataPath = dataPath;
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (IsInitialized) return;
            
            Logger.LogInformation("Initializing JSON Vector Store at {DataPath}", _dataPath);
            
            Directory.CreateDirectory(Path.GetDirectoryName(_dataPath) ?? "./data");
            
            if (File.Exists(_dataPath))
            {
                LoadFromFile();
            }
            else
            {
                Logger.LogInformation("Creating new empty vector store");
                SaveToFile();
            }
            
            IsInitialized = true;
            Logger.LogInformation("JSON Vector Store initialized with {Count} vectors", _vectors.Count);
        }
        
        await Task.CompletedTask;
    }

    public override Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureInitialized();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public override Task UpsertAsync(Chunk chunk, float[] embedding, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        lock (_lock)
        {
            var entry = new VectorEntry
            {
                Id = chunk.Id,
                Chunk = chunk,
                Embedding = embedding,
                UpdatedAt = DateTime.UtcNow
            };
            
            _vectors[chunk.Id] = entry;
            SaveToFile();
            
            Logger.LogDebug("Upserted vector for chunk {ChunkId}", chunk.Id);
        }
        
        return Task.CompletedTask;
    }

    public override Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK = 10, double minScore = 0.0, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        lock (_lock)
        {
            var results = new List<(string id, double score, Chunk chunk)>();
            
            foreach (var entry in _vectors.Values)
            {
                var similarity = CalculateCosineSimilarity(queryEmbedding, entry.Embedding);
                
                if (similarity >= minScore)
                {
                    results.Add((entry.Id, similarity, entry.Chunk));
                }
            }
            
            var topResults = results
                .OrderByDescending(r => r.score)
                .Take(topK)
                .Select(r => VectorSearchResult.Create(r.id, r.score, r.chunk))
                .ToList();
            
            Logger.LogDebug("Search returned {Count} results from {Total} vectors", topResults.Count, _vectors.Count);
            
            return Task.FromResult(topResults);
        }
    }

    public override Task<Chunk?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        lock (_lock)
        {
            return Task.FromResult(_vectors.TryGetValue(id, out var entry) ? entry.Chunk : null);
        }
    }

    public override Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        lock (_lock)
        {
            var removed = _vectors.Remove(id);
            if (removed)
            {
                SaveToFile();
                Logger.LogDebug("Deleted vector {Id}", id);
            }
            return Task.FromResult(removed);
        }
    }

    public override Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        lock (_lock)
        {
            return Task.FromResult((long)_vectors.Count);
        }
    }

    private void LoadFromFile()
    {
        try
        {
            var json = File.ReadAllText(_dataPath);
            var entries = JsonSerializer.Deserialize<Dictionary<string, VectorEntry>>(json, JsonOptions);
            
            if (entries != null)
            {
                _vectors.Clear();
                foreach (var kvp in entries)
                {
                    _vectors[kvp.Key] = kvp.Value;
                }
            }
            
            Logger.LogDebug("Loaded {Count} vectors from {Path}", _vectors.Count, _dataPath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load vectors from {Path}", _dataPath);
            throw;
        }
    }

    private void SaveToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_vectors, JsonOptions);
            File.WriteAllText(_dataPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save vectors to {Path}", _dataPath);
            throw;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private class VectorEntry
    {
        public string Id { get; set; } = string.Empty;
        public Chunk Chunk { get; set; } = new();
        public float[] Embedding { get; set; } = [];
        public DateTime UpdatedAt { get; set; }
    }
}
