using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using System.Collections.Concurrent;

namespace OHS.Copilot.Infrastructure.Services;

public class InMemoryMemoryService : IMemoryService
{
    private readonly ConcurrentDictionary<string, ConversationMemory> _conversations = new();
    private readonly ConcurrentDictionary<string, PersonaMemory> _personas = new();
    private readonly ConcurrentDictionary<string, PolicyMemory> _policies = new();
    private readonly ILogger<InMemoryMemoryService> _logger;

    public InMemoryMemoryService(ILogger<InMemoryMemoryService> logger)
    {
        _logger = logger;
        InitializeDefaultPolicies();
    }

    public Task SaveConversationMemoryAsync(string conversationId, ConversationMemory memory, CancellationToken cancellationToken = default)
    {
        _conversations[conversationId] = memory;
        _logger.LogDebug("Saved conversation memory for {ConversationId} with {TurnCount} turns", 
            conversationId, memory.Turns.Count);
        return Task.CompletedTask;
    }

    public Task<ConversationMemory?> GetConversationMemoryAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        _conversations.TryGetValue(conversationId, out var memory);
        if (memory != null)
        {
            _logger.LogDebug("Retrieved conversation memory for {ConversationId} with {TurnCount} turns",
                conversationId, memory.Turns.Count);
        }
        return Task.FromResult(memory);
    }

    public async Task UpdateConversationMemoryAsync(string conversationId, string userMessage, string assistantResponse, CancellationToken cancellationToken = default)
    {
        var memory = await GetConversationMemoryAsync(conversationId, cancellationToken) 
            ?? ConversationMemory.Create(conversationId);

        memory.AddTurn(userMessage, assistantResponse);
        await SaveConversationMemoryAsync(conversationId, memory, cancellationToken);

        _logger.LogDebug("Updated conversation memory for {ConversationId}, now has {TurnCount} turns",
            conversationId, memory.Turns.Count);
    }

    public Task DeleteConversationMemoryAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var removed = _conversations.TryRemove(conversationId, out _);
        if (removed)
        {
            _logger.LogDebug("Deleted conversation memory for {ConversationId}", conversationId);
        }
        return Task.CompletedTask;
    }

    public Task SavePersonaMemoryAsync(string userId, PersonaMemory memory, CancellationToken cancellationToken = default)
    {
        _personas[userId] = memory;
        _logger.LogDebug("Saved persona memory for user {UserId} as {PersonaType}", userId, memory.Type);
        return Task.CompletedTask;
    }

    public Task<PersonaMemory?> GetPersonaMemoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        _personas.TryGetValue(userId, out var memory);
        return Task.FromResult(memory);
    }

    public async Task UpdatePersonaMemoryAsync(string userId, Dictionary<string, string> facts, CancellationToken cancellationToken = default)
    {
        var memory = await GetPersonaMemoryAsync(userId, cancellationToken);
        if (memory == null)
        {
            memory = PersonaMemory.Create(userId, PersonaType.Inspector);
            await SavePersonaMemoryAsync(userId, memory, cancellationToken);
        }

        foreach (var fact in facts)
        {
            memory.Profile[fact.Key] = fact.Value;
        }

        memory.LastUpdated = DateTime.UtcNow;
        await SavePersonaMemoryAsync(userId, memory, cancellationToken);

        _logger.LogDebug("Updated persona memory for user {UserId} with {FactCount} facts", userId, facts.Count);
    }

    public Task SavePolicyMemoryAsync(string policyKey, PolicyMemory memory, CancellationToken cancellationToken = default)
    {
        _policies[policyKey] = memory;
        _logger.LogDebug("Saved policy memory for {PolicyKey}: {Title}", policyKey, memory.Title);
        return Task.CompletedTask;
    }

    public Task<List<PolicyMemory>> SearchPolicyMemoryAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        var searchLower = searchTerm.ToLower();
        var results = _policies.Values
            .Where(policy => 
                policy.Title.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                policy.Content.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                policy.Tags.Any(tag => tag.Contains(searchLower, StringComparison.OrdinalIgnoreCase)) ||
                policy.Category.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(policy => policy.AccessCount)
            .ThenByDescending(policy => policy.LastAccessed)
            .Take(maxResults)
            .ToList();

        foreach (var policy in results)
        {
            policy.RecordAccess();
        }

        _logger.LogDebug("Policy search for '{SearchTerm}' returned {ResultCount} results", searchTerm, results.Count);
        return Task.FromResult(results);
    }

    public Task<Dictionary<string, PolicyMemory>> GetAllPolicyMemoryAsync(CancellationToken cancellationToken = default)
    {
        var allPolicies = _policies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return Task.FromResult(allPolicies);
    }

    public Task CleanupExpiredMemoryAsync(TimeSpan conversationTtl, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow - conversationTtl;
        var expiredConversations = _conversations
            .Where(kvp => kvp.Value.LastActivity < cutoffDate)
            .Select(kvp => kvp.Key)
            .ToList();

        var removedCount = 0;
        foreach (var conversationId in expiredConversations)
        {
            if (_conversations.TryRemove(conversationId, out _))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired conversation memories older than {TTL}", 
                removedCount, conversationTtl);
        }

        return Task.CompletedTask;
    }

    private void InitializeDefaultPolicies()
    {
        var defaultPolicies = new List<PolicyMemory>
        {
            PolicyMemory.Create(
                key: "ppe_requirements",
                title: "Personal Protective Equipment Requirements",
                content: "Hard hats, safety glasses, and steel-toed boots are required in construction areas. All PPE must meet ANSI standards.",
                tags: ["PPE", "safety", "construction", "equipment"],
                category: "safety"
            ),
            
            PolicyMemory.Create(
                key: "incident_reporting",
                title: "Incident Reporting Procedures", 
                content: "All workplace incidents must be reported within 24 hours using Form WS-101. Include date, time, location, personnel, and witness statements.",
                tags: ["incident", "reporting", "procedures", "forms"],
                category: "safety"
            ),
            
            PolicyMemory.Create(
                key: "return_to_work",
                title: "Return to Work Guidelines",
                content: "Medical clearance required from healthcare provider. Gradual return program available with modified duties and reduced hours as needed.",
                tags: ["return to work", "medical", "clearance", "accommodations"],
                category: "medical"
            ),
            
            PolicyMemory.Create(
                key: "emergency_procedures",
                title: "Emergency Response Procedures",
                content: "Fire: activate alarm, evacuate via nearest exit, proceed to muster point. Medical: call 911, provide first aid if trained, do not move injured unless in danger.",
                tags: ["emergency", "fire", "medical", "evacuation", "procedures"],
                category: "emergency"
            ),
            
            PolicyMemory.Create(
                key: "chemical_safety",
                title: "Chemical Handling Protocols",
                content: "Store chemicals in appropriate containers with proper labeling. Only trained personnel may handle hazardous materials. PPE required at all times.",
                tags: ["chemicals", "hazardous materials", "storage", "handling", "training"],
                category: "safety"
            )
        };

        foreach (var policy in defaultPolicies)
        {
            _policies[policy.Key] = policy;
        }

        _logger.LogInformation("Initialized {Count} default policy memories", defaultPolicies.Count);
    }

    public void ExportMemoryData(string basePath)
    {
        try
        {
            Directory.CreateDirectory(basePath);

            var conversationsPath = Path.Combine(basePath, "conversations.json");
            var conversationsJson = System.Text.Json.JsonSerializer.Serialize(_conversations.Values.ToList(), JsonOptions);
            File.WriteAllText(conversationsPath, conversationsJson);

            var personasPath = Path.Combine(basePath, "personas.json");
            var personasJson = System.Text.Json.JsonSerializer.Serialize(_personas.Values.ToList(), JsonOptions);
            File.WriteAllText(personasPath, personasJson);

            var policiesPath = Path.Combine(basePath, "policies.json");
            var policiesJson = System.Text.Json.JsonSerializer.Serialize(_policies.Values.ToList(), JsonOptions);
            File.WriteAllText(policiesPath, policiesJson);

            _logger.LogInformation("Exported memory data: {Conversations} conversations, {Personas} personas, {Policies} policies",
                _conversations.Count, _personas.Count, _policies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export memory data to {BasePath}", basePath);
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };
}
