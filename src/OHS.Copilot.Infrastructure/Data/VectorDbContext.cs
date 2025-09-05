using Microsoft.EntityFrameworkCore;
using OHS.Copilot.Domain.Entities;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace OHS.Copilot.Infrastructure.Data;

public class VectorDbContext : DbContext
{
    public VectorDbContext(DbContextOptions<VectorDbContext> options) : base(options)
    {
    }

    public DbSet<ChunkEntity> Chunks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<ChunkEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Section).HasMaxLength(200);
            entity.Property(e => e.SourcePath).HasMaxLength(500);
            entity.Property(e => e.Hash).HasMaxLength(64);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Hash);
            entity.HasIndex(e => e.SourcePath);
            entity.HasIndex(e => e.Embedding).HasMethod("ivfflat").HasOperators("vector_cosine_ops");
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(o => o.UseVector());
    }
}

public class ChunkEntity
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = default!; 
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Metadata { get; set; }

    public static ChunkEntity FromDomainChunk(Chunk chunk, float[] embedding)
    {
        return new ChunkEntity
        {
            Id = chunk.Id,
            Text = chunk.Text,
            Title = chunk.Title,
            Section = chunk.Section,
            SourcePath = chunk.SourcePath,
            Hash = chunk.Hash,
            Embedding = new Vector(embedding),
            CreatedAt = chunk.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            Metadata = System.Text.Json.JsonSerializer.Serialize(chunk.Metadata)
        };
    }

    public Chunk ToDomainChunk()
    {
        var chunk = new Chunk
        {
            Id = Id,
            Text = Text,
            Title = Title,
            Section = Section,
            SourcePath = SourcePath,
            Hash = Hash,
            CreatedAt = CreatedAt
        };

        if (!string.IsNullOrEmpty(Metadata))
        {
            try
            {
                var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(Metadata);
                if (metadata != null)
                {
                    chunk.Metadata = metadata;
                }
            }
            catch
            {
                // Ignore metadata deserialization errors
            }
        }

        return chunk;
    }
}
