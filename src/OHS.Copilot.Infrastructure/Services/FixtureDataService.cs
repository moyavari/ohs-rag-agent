using System.Text.Json;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace OHS.Copilot.Infrastructure.Services;

public class FixtureDataService
{
    private readonly ILogger<FixtureDataService> _logger;
    private readonly string _fixturesPath;

    public FixtureDataService(ILogger<FixtureDataService> logger, string fixturesPath = "./fixtures")
    {
        _logger = logger;
        _fixturesPath = fixturesPath;
    }

    public async Task LoadSeedDataAsync(IVectorStore vectorStore, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading seed data from {Path}", _fixturesPath);
        
        var seedDataPath = Path.Combine(_fixturesPath, "seed-data.json");
        
        if (!File.Exists(seedDataPath))
        {
            _logger.LogWarning("Seed data file not found at {Path}, creating sample data", seedDataPath);
            await CreateSampleSeedDataAsync(seedDataPath, cancellationToken);
        }

        var seedData = await LoadSeedDataFromFileAsync(seedDataPath, cancellationToken);
        
        foreach (var item in seedData.Items)
        {
            await vectorStore.UpsertAsync(item.Chunk, item.Embedding, cancellationToken);
        }
        
        _logger.LogInformation("Loaded {Count} seed vectors into vector store", seedData.Items.Count);
    }

    private async Task<SeedData> LoadSeedDataFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var seedData = JsonSerializer.Deserialize<SeedData>(json, JsonOptions);
            return seedData ?? new SeedData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load seed data from {Path}", filePath);
            return new SeedData();
        }
    }

    private async Task CreateSampleSeedDataAsync(string filePath, CancellationToken cancellationToken)
    {
        var sampleData = new SeedData
        {
            Items =
            [
                CreateSeedItem("1", "Safety Equipment Requirements", "Personal Protective Equipment", 
                    "All workers must wear appropriate safety equipment including hard hats, safety glasses, and steel-toed boots when working in construction areas."),
                
                CreateSeedItem("2", "Incident Reporting Procedures", "Workplace Safety", 
                    "Any workplace incident must be reported within 24 hours to the safety coordinator. This includes near-misses, injuries, and equipment damage."),
                
                CreateSeedItem("3", "Return to Work Guidelines", "Medical Leave", 
                    "Employees returning from medical leave must provide medical clearance and may require gradual return-to-work accommodations."),
                
                CreateSeedItem("4", "Emergency Evacuation Plan", "Emergency Procedures", 
                    "In case of emergency, all personnel must evacuate via designated routes and assemble at the primary muster point in the parking lot."),
                
                CreateSeedItem("5", "Chemical Handling Protocols", "Hazardous Materials", 
                    "Chemical substances must be stored in appropriate containers, labeled correctly, and handled only by trained personnel with proper PPE."),
            ]
        };

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "./fixtures");
        
        var json = JsonSerializer.Serialize(sampleData, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        
        _logger.LogInformation("Created sample seed data at {Path}", filePath);
    }

    private static SeedDataItem CreateSeedItem(string id, string title, string section, string text)
    {
        var chunk = Chunk.Create(text, title, section, $"sample-doc-{id}.pdf");
        chunk.Id = id;
        
        var embedding = GenerateRandomEmbedding(1536);
        
        return new SeedDataItem
        {
            Chunk = chunk,
            Embedding = embedding
        };
    }

    private static float[] GenerateRandomEmbedding(int dimensions)
    {
        var random = new Random(42);
        var embedding = new float[dimensions];
        
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }
        
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(embedding[i] / magnitude);
        }
        
        return embedding;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

public class SeedData
{
    public List<SeedDataItem> Items { get; set; } = [];
}

public class SeedDataItem
{
    public Chunk Chunk { get; set; } = new();
    public float[] Embedding { get; set; } = [];
}
