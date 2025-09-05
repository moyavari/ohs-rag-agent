using OHS.Copilot.Infrastructure.Configuration;

namespace OHS.Copilot.Infrastructure.VectorStores;

public class VectorStoreConnectionFactory
{
    private readonly AppSettings _settings;

    public VectorStoreConnectionFactory(AppSettings settings)
    {
        _settings = settings;
    }

    public VectorStoreConnection CreateConnection(string storeType)
    {
        return storeType.ToLower() switch
        {
            "qdrant" => CreateQdrantConnection(),
            "pgvector" or "postgres" => CreatePostgresConnection(),
            "cosmos" or "cosmosdb" => CreateCosmosConnection(),
            _ => throw new ArgumentException($"Unsupported vector store type: {storeType}")
        };
    }

    private VectorStoreConnection CreateQdrantConnection()
    {
        return new VectorStoreConnection
        {
            Type = "qdrant",
            ConnectionString = _settings.Qdrant.Endpoint,
            ApiKey = _settings.Qdrant.ApiKey,
            DatabaseName = "default",
            CollectionName = "ohs_chunks"
        };
    }

    private VectorStoreConnection CreatePostgresConnection()
    {
        return new VectorStoreConnection
        {
            Type = "postgres",
            ConnectionString = _settings.PostgreSQL.ConnectionString,
            DatabaseName = ExtractDatabaseName(_settings.PostgreSQL.ConnectionString),
            CollectionName = "chunks"
        };
    }

    private VectorStoreConnection CreateCosmosConnection()
    {
        return new VectorStoreConnection
        {
            Type = "cosmos",
            ConnectionString = _settings.CosmosDb.ConnectionString,
            DatabaseName = "OHSCopilot",
            CollectionName = "chunks"
        };
    }

    private static string ExtractDatabaseName(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "ohscopilot";

        var parts = connectionString.Split(';');
        var dbPart = parts.FirstOrDefault(p => p.Trim().StartsWith("Database=", StringComparison.OrdinalIgnoreCase));
        
        if (dbPart != null)
        {
            return dbPart.Split('=')[1].Trim();
        }

        return "ohscopilot";
    }
}

public class VectorStoreConnection
{
    public string Type { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
}
