using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using System.Collections.Concurrent;

namespace OHS.Copilot.Infrastructure.Services;

public class InMemoryPromptVersionService : IPromptVersionService
{
    private readonly ConcurrentDictionary<string, PromptVersion> _promptsByHash = new();
    private readonly ConcurrentDictionary<string, List<PromptVersion>> _promptsByName = new();
    private readonly ILogger<InMemoryPromptVersionService> _logger;

    public InMemoryPromptVersionService(ILogger<InMemoryPromptVersionService> logger)
    {
        _logger = logger;
    }

    public Task<string> StorePromptAsync(string promptContent, string promptName = "default", CancellationToken cancellationToken = default)
    {
        var hash = PromptVersion.ComputeHash(promptContent);
        
        if (_promptsByHash.ContainsKey(hash))
        {
            _logger.LogDebug("Prompt hash {Hash} already exists for {PromptName}", hash, promptName);
            return Task.FromResult(hash);
        }

        var existingVersions = _promptsByName.GetOrAdd(promptName, _ => []);
        var version = existingVersions.Count + 1;

        var promptVersion = PromptVersion.Create(hash, promptName, promptContent, version);
        
        _promptsByHash[hash] = promptVersion;
        existingVersions.Add(promptVersion);

        _logger.LogInformation("Stored new prompt version: {PromptName} v{Version} with hash {Hash}",
            promptName, version, hash);

        return Task.FromResult(hash);
    }

    public Task<PromptVersion?> GetPromptByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        _promptsByHash.TryGetValue(hash, out var prompt);
        return Task.FromResult(prompt);
    }

    public Task<List<PromptVersion>> GetPromptHistoryAsync(string promptName, CancellationToken cancellationToken = default)
    {
        var history = _promptsByName.TryGetValue(promptName, out var versions) 
            ? versions.OrderByDescending(v => v.Version).ToList() 
            : [];

        _logger.LogDebug("Retrieved {Count} versions for prompt {PromptName}", history.Count, promptName);

        return Task.FromResult(history);
    }

    public Task<Dictionary<string, string>> GetAllPromptHashesAsync(CancellationToken cancellationToken = default)
    {
        var hashes = _promptsByHash.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Name
        );

        return Task.FromResult(hashes);
    }

    public void ExportPrompts(string filePath)
    {
        try
        {
            var allPrompts = _promptsByHash.Values.OrderBy(p => p.Name).ThenBy(p => p.Version).ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(allPrompts, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(filePath, json);
            
            _logger.LogInformation("Exported {Count} prompt versions to {FilePath}", allPrompts.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export prompts to {FilePath}", filePath);
        }
    }
}
