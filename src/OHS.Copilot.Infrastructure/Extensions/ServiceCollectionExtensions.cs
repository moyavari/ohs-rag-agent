using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Configuration;
using OHS.Copilot.Infrastructure.Data;
using OHS.Copilot.Infrastructure.Services;
using OHS.Copilot.Infrastructure.VectorStores;
using Qdrant.Client;

namespace OHS.Copilot.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVectorStore(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton<VectorStoreConnectionFactory>();
        services.AddSingleton<VectorStoreFactory>();
        services.AddSingleton<FixtureDataService>();

        var storeType = settings.VectorStore.Type.ToLower();
        
        return storeType switch
        {
            "json" => services.AddJsonVectorStore(settings),
            "qdrant" => services.AddQdrantVectorStore(settings),
            "pgvector" or "postgres" => services.AddPostgresVectorStore(settings),
            "cosmos" or "cosmosdb" => services.AddCosmosVectorStore(settings),
            _ => throw new ArgumentException($"Unsupported vector store type: {storeType}")
        };
    }

    private static IServiceCollection AddJsonVectorStore(this IServiceCollection services, AppSettings settings)
    {
        services.AddTransient<IVectorStore>(provider =>
        {
            var factory = provider.GetRequiredService<VectorStoreFactory>();
            return factory.CreateVectorStoreAsync().GetAwaiter().GetResult();
        });

        return services;
    }

    private static IServiceCollection AddQdrantVectorStore(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton<QdrantClient>(provider =>
        {
            return new QdrantClient(settings.Qdrant.Endpoint, apiKey: settings.Qdrant.ApiKey);
        });

        services.AddTransient<IVectorStore>(provider =>
        {
            var factory = provider.GetRequiredService<VectorStoreFactory>();
            return factory.CreateVectorStoreAsync().GetAwaiter().GetResult();
        });

        return services;
    }

    private static IServiceCollection AddPostgresVectorStore(this IServiceCollection services, AppSettings settings)
    {
        services.AddDbContext<VectorDbContext>(options =>
        {
            options.UseNpgsql(settings.PostgreSQL.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseVector();
            });
        });

        services.AddTransient<IVectorStore>(provider =>
        {
            var factory = provider.GetRequiredService<VectorStoreFactory>();
            return factory.CreateVectorStoreAsync().GetAwaiter().GetResult();
        });

        return services;
    }

    private static IServiceCollection AddCosmosVectorStore(this IServiceCollection services, AppSettings settings)
    {
        if (string.IsNullOrEmpty(settings.CosmosDb.ConnectionString))
        {
            throw new InvalidOperationException("Cosmos DB connection string is required when using cosmos vector store");
        }
        
        services.AddSingleton<CosmosClient>(provider =>
        {
            var cosmosClientOptions = new CosmosClientOptions
            {
                ApplicationName = "OHS.Copilot",
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = ConsistencyLevel.Session
            };

            return new CosmosClient(settings.CosmosDb.ConnectionString, cosmosClientOptions);
        });

        services.AddTransient<IVectorStore>(provider =>
        {
            var factory = provider.GetRequiredService<VectorStoreFactory>();
            return factory.CreateVectorStoreAsync().GetAwaiter().GetResult();
        });

        return services;
    }

    public static IServiceCollection AddEmbeddingService(this IServiceCollection services, AppSettings settings)
    {
        if (settings.DemoMode || settings.VectorStore.Type.ToLower() == "json")
        {
            services.AddSingleton<IEmbeddingService, DemoEmbeddingService>();
        }
        else
        {
            services.AddSingleton<IEmbeddingService, Services.AzureOpenAIEmbeddingService>();
        }

        return services;
    }
}

public class DemoEmbeddingService : IEmbeddingService
{
    private readonly Random _random = new(42);

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embedding = GenerateRandomEmbedding(1536);
        return Task.FromResult(embedding);
    }

    public Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var embeddings = texts.Select(_ => GenerateRandomEmbedding(1536)).ToList();
        return Task.FromResult(embeddings);
    }

    public int GetEmbeddingDimensions() => 1536;

    public string GetModelName() => "demo-embedding-model";

    private float[] GenerateRandomEmbedding(int dimensions)
    {
        var embedding = new float[dimensions];
        
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
        }
        
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(embedding[i] / magnitude);
        }
        
        return embedding;
    }
}

