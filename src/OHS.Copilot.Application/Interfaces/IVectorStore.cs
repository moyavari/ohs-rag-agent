using OHS.Copilot.Domain.Entities;

namespace OHS.Copilot.Application.Interfaces;

public interface IVectorStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
    
    Task UpsertAsync(Chunk chunk, float[] embedding, CancellationToken cancellationToken = default);
    Task UpsertBatchAsync(IEnumerable<(Chunk chunk, float[] embedding)> items, CancellationToken cancellationToken = default);
    
    Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK = 10, double minScore = 0.0, CancellationToken cancellationToken = default);
    
    Task<Chunk?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(CancellationToken cancellationToken = default);
}

public class VectorSearchResult
{
    public string Id { get; set; } = string.Empty;
    public double Score { get; set; }
    public Chunk Chunk { get; set; } = new();
    
    public static VectorSearchResult Create(string id, double score, Chunk chunk)
    {
        return new VectorSearchResult
        {
            Id = id,
            Score = score,
            Chunk = chunk
        };
    }
}
