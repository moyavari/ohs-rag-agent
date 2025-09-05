using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.Entities;
using OHS.Copilot.Infrastructure.Data;
using Pgvector;

namespace OHS.Copilot.Infrastructure.VectorStores;

public class PostgresVectorStore : VectorStoreBase
{
    private readonly VectorDbContext _context;

    public PostgresVectorStore(ILogger<PostgresVectorStore> logger, VectorDbContext context) 
        : base(logger)
    {
        _context = context;
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized) return;

        Logger.LogInformation("Initializing PostgreSQL Vector Store");

        try
        {
            await _context.Database.EnsureCreatedAsync(cancellationToken);
            
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                Logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                await _context.Database.MigrateAsync(cancellationToken);
            }

            IsInitialized = true;
            Logger.LogInformation("PostgreSQL Vector Store initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize PostgreSQL Vector Store");
            throw;
        }
    }

    public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "PostgreSQL health check failed");
            return false;
        }
    }

    public override async Task UpsertAsync(Chunk chunk, float[] embedding, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var existingEntity = await _context.Chunks.FindAsync([chunk.Id], cancellationToken);
            
            if (existingEntity != null)
            {
                existingEntity.Text = chunk.Text;
                existingEntity.Title = chunk.Title;
                existingEntity.Section = chunk.Section;
                existingEntity.SourcePath = chunk.SourcePath;
                existingEntity.Hash = chunk.Hash;
                existingEntity.Embedding = new Vector(embedding);
                existingEntity.UpdatedAt = DateTime.UtcNow;
                existingEntity.Metadata = System.Text.Json.JsonSerializer.Serialize(chunk.Metadata);
                
                _context.Chunks.Update(existingEntity);
            }
            else
            {
                var entity = ChunkEntity.FromDomainChunk(chunk, embedding);
                _context.Chunks.Add(entity);
            }

            await _context.SaveChangesAsync(cancellationToken);
            Logger.LogDebug("Upserted chunk {ChunkId} to PostgreSQL", chunk.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to upsert chunk {ChunkId} to PostgreSQL", chunk.Id);
            throw;
        }
    }

    public override async Task UpsertBatchAsync(IEnumerable<(Chunk chunk, float[] embedding)> items, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var itemList = items.ToList();
            var chunkIds = itemList.Select(item => item.chunk.Id).ToList();
            
            var existingEntities = await _context.Chunks
                .Where(c => chunkIds.Contains(c.Id))
                .ToListAsync(cancellationToken);
            
            var existingIds = existingEntities.Select(e => e.Id).ToHashSet();

            foreach (var (chunk, embedding) in itemList)
            {
                var existingEntity = existingEntities.FirstOrDefault(e => e.Id == chunk.Id);
                
                if (existingEntity != null)
                {
                    existingEntity.Text = chunk.Text;
                    existingEntity.Title = chunk.Title;
                    existingEntity.Section = chunk.Section;
                    existingEntity.SourcePath = chunk.SourcePath;
                    existingEntity.Hash = chunk.Hash;
                    existingEntity.Embedding = new Vector(embedding);
                    existingEntity.UpdatedAt = DateTime.UtcNow;
                    existingEntity.Metadata = System.Text.Json.JsonSerializer.Serialize(chunk.Metadata);
                    
                    _context.Chunks.Update(existingEntity);
                }
                else
                {
                    var entity = ChunkEntity.FromDomainChunk(chunk, embedding);
                    _context.Chunks.Add(entity);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            Logger.LogDebug("Batch upserted {Count} chunks to PostgreSQL", itemList.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to batch upsert {Count} chunks to PostgreSQL", items.Count());
            throw;
        }
    }

    public override async Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK = 10, double minScore = 0.0, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var queryVector = new Vector(queryEmbedding);
            
            // For now, we'll fetch all and calculate similarity in memory
            // This is inefficient but will work until we get the pgvector distance function working correctly
            var allChunks = await _context.Chunks.ToListAsync(cancellationToken);
            
            var results = allChunks
                .Select(c => new
                {
                    Entity = c,
                    Distance = 1.0 - CalculateCosineSimilarity(queryVector.ToArray(), c.Embedding.ToArray())
                })
                .OrderBy(x => x.Distance)
                .Take(topK);

            var filteredResults = results
                .Where(r => minScore <= 0.0 || (1.0 - r.Distance) >= minScore);

            var vectorResults = filteredResults
                .Select(r => VectorSearchResult.Create(r.Entity.Id, 1.0 - r.Distance, r.Entity.ToDomainChunk()))
                .ToList();

            Logger.LogDebug("Search returned {Count} results from PostgreSQL", vectorResults.Count);
            return vectorResults;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to search PostgreSQL vector store");
            throw;
        }
    }

    public override async Task<Chunk?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var entity = await _context.Chunks.FindAsync([id], cancellationToken);
            return entity?.ToDomainChunk();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get chunk {ChunkId} from PostgreSQL", id);
            throw;
        }
    }

    public override async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var entity = await _context.Chunks.FindAsync([id], cancellationToken);
            if (entity == null) return false;

            _context.Chunks.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            
            Logger.LogDebug("Deleted chunk {ChunkId} from PostgreSQL", id);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete chunk {ChunkId} from PostgreSQL", id);
            return false;
        }
    }

    public override async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            return await _context.Chunks.LongCountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get count from PostgreSQL vector store");
            throw;
        }
    }
}
