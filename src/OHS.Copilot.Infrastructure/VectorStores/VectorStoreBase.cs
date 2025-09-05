using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace OHS.Copilot.Infrastructure.VectorStores;

public abstract class VectorStoreBase : IVectorStore
{
    protected readonly ILogger Logger;
    protected bool IsInitialized = false;

    protected VectorStoreBase(ILogger logger)
    {
        Logger = logger;
    }

    public abstract Task InitializeAsync(CancellationToken cancellationToken = default);
    public abstract Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
    
    public virtual async Task UpsertBatchAsync(IEnumerable<(Chunk chunk, float[] embedding)> items, CancellationToken cancellationToken = default)
    {
        foreach (var (chunk, embedding) in items)
        {
            await UpsertAsync(chunk, embedding, cancellationToken);
        }
    }

    public abstract Task UpsertAsync(Chunk chunk, float[] embedding, CancellationToken cancellationToken = default);
    public abstract Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK = 10, double minScore = 0.0, CancellationToken cancellationToken = default);
    public abstract Task<Chunk?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    public abstract Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    public abstract Task<long> GetCountAsync(CancellationToken cancellationToken = default);

    protected virtual void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException($"{GetType().Name} has not been initialized. Call InitializeAsync first.");
        }
    }

    protected virtual double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
        {
            throw new ArgumentException("Vectors must have the same dimensions");
        }

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}
