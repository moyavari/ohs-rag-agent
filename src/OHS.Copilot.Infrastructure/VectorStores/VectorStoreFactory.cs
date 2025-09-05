using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Configuration;
using OHS.Copilot.Infrastructure.Data;
using OHS.Copilot.Infrastructure.Services;
using Qdrant.Client;

namespace OHS.Copilot.Infrastructure.VectorStores;

public class VectorStoreFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppSettings _settings;
    private readonly ILogger<VectorStoreFactory> _logger;

    public VectorStoreFactory(IServiceProvider serviceProvider, AppSettings settings, ILogger<VectorStoreFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IVectorStore> CreateVectorStoreAsync(CancellationToken cancellationToken = default)
    {
        var storeType = _settings.VectorStore.Type.ToLower();

        IVectorStore vectorStore = storeType switch
        {
            "json" => CreateJsonVectorStore(),
            "qdrant" => CreateQdrantVectorStore(),
            "pgvector" or "postgres" => CreatePostgresVectorStore(),
            "cosmos" or "cosmosdb" => CreateCosmosVectorStore(),
            _ => throw new ArgumentException($"Unsupported vector store type: {storeType}")
        };

        await vectorStore.InitializeAsync(cancellationToken);

        if (_settings.DemoMode && storeType == "json")
        {
            await LoadDemoDataAsync(vectorStore, cancellationToken);
        }

        return vectorStore;
    }

    private IVectorStore CreateJsonVectorStore()
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<JsonVectorStore>>();
        var dataPath = Path.Combine(_settings.Demo.FixturesPath, "vectors.json");
        return new JsonVectorStore(logger, dataPath);
    }

    private IVectorStore CreateQdrantVectorStore()
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<QdrantVectorStore>>();
        
        var client = new QdrantClient(_settings.Qdrant.Endpoint, apiKey: _settings.Qdrant.ApiKey);
        
        return new QdrantVectorStore(logger, client);
    }

    private IVectorStore CreatePostgresVectorStore()
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<PostgresVectorStore>>();
        var context = _serviceProvider.GetRequiredService<VectorDbContext>();
        
        return new PostgresVectorStore(logger, context);
    }

    private IVectorStore CreateCosmosVectorStore()
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<CosmosVectorStore>>();
        var cosmosClient = _serviceProvider.GetRequiredService<CosmosClient>();
        
        return new CosmosVectorStore(logger, cosmosClient);
    }

    private async Task LoadDemoDataAsync(IVectorStore vectorStore, CancellationToken cancellationToken)
    {
        try
        {
            var fixtureService = _serviceProvider.GetRequiredService<FixtureDataService>();
            await fixtureService.LoadSeedDataAsync(vectorStore, cancellationToken);
            _logger.LogInformation("Demo data loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load demo data, continuing without it");
        }
    }
}
