namespace OHS.Copilot.Application.Interfaces;

public interface IMemoryService
{
    Task SaveConversationMemoryAsync(string conversationId, ConversationMemory memory, CancellationToken cancellationToken = default);
    Task<ConversationMemory?> GetConversationMemoryAsync(string conversationId, CancellationToken cancellationToken = default);
    Task UpdateConversationMemoryAsync(string conversationId, string userMessage, string assistantResponse, CancellationToken cancellationToken = default);
    Task DeleteConversationMemoryAsync(string conversationId, CancellationToken cancellationToken = default);
    
    Task SavePersonaMemoryAsync(string userId, PersonaMemory memory, CancellationToken cancellationToken = default);
    Task<PersonaMemory?> GetPersonaMemoryAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdatePersonaMemoryAsync(string userId, Dictionary<string, string> facts, CancellationToken cancellationToken = default);
    
    Task SavePolicyMemoryAsync(string policyKey, PolicyMemory memory, CancellationToken cancellationToken = default);
    Task<List<PolicyMemory>> SearchPolicyMemoryAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<Dictionary<string, PolicyMemory>> GetAllPolicyMemoryAsync(CancellationToken cancellationToken = default);
    
    Task CleanupExpiredMemoryAsync(TimeSpan conversationTtl, CancellationToken cancellationToken = default);
}

public class ConversationMemory
{
    public string ConversationId { get; set; } = string.Empty;
    public List<ConversationTurn> Turns { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];

    public static ConversationMemory Create(string conversationId, string? userId = null)
    {
        return new ConversationMemory
        {
            ConversationId = conversationId,
            UserId = userId
        };
    }

    public void AddTurn(string userMessage, string assistantResponse, List<string>? citationIds = null)
    {
        Turns.Add(new ConversationTurn
        {
            UserMessage = userMessage,
            AssistantResponse = assistantResponse,
            CitationIds = citationIds ?? [],
            Timestamp = DateTime.UtcNow
        });
        
        LastActivity = DateTime.UtcNow;

        if (Turns.Count > 10)
        {
            Turns = Turns.TakeLast(10).ToList();
        }
    }

    public string GetRecentContext(int maxTurns = 3)
    {
        var recentTurns = Turns.TakeLast(maxTurns);
        var context = recentTurns.Select(turn => $"User: {turn.UserMessage}\nAssistant: {turn.AssistantResponse}");
        return string.Join("\n\n", context);
    }
}

public class ConversationTurn
{
    public string UserMessage { get; set; } = string.Empty;
    public string AssistantResponse { get; set; } = string.Empty;
    public List<string> CitationIds { get; set; } = [];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class PersonaMemory
{
    public string UserId { get; set; } = string.Empty;
    public PersonaType Type { get; set; } = PersonaType.Inspector;
    public Dictionary<string, string> Profile { get; set; } = [];
    public List<string> Preferences { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public static PersonaMemory Create(string userId, PersonaType type)
    {
        var memory = new PersonaMemory
        {
            UserId = userId,
            Type = type
        };
        
        memory.InitializeDefaults();
        return memory;
    }

    public void InitializeDefaults()
    {
        Profile = Type switch
        {
            PersonaType.Inspector => new Dictionary<string, string>
            {
                ["Role"] = "Field Inspector",
                ["ResponseStyle"] = "Concise and direct",
                ["PreferredSources"] = "Policy documents, field guides",
                ["TypicalQuestions"] = "Equipment requirements, compliance checks"
            },
            PersonaType.ClaimsAdjudicator => new Dictionary<string, string>
            {
                ["Role"] = "Claims Adjudicator", 
                ["ResponseStyle"] = "Detailed and neutral",
                ["PreferredSources"] = "Policies, medical guidelines, case precedents",
                ["TypicalQuestions"] = "Return-to-work, medical assessments, claim decisions"
            },
            PersonaType.PolicyAnalyst => new Dictionary<string, string>
            {
                ["Role"] = "Medical/Policy Analyst",
                ["ResponseStyle"] = "Comprehensive with multiple sources",
                ["PreferredSources"] = "Research, policy analysis, regulatory documents", 
                ["TypicalQuestions"] = "Policy interpretation, regulatory compliance, analysis"
            },
            _ => []
        };

        Preferences = Type switch
        {
            PersonaType.Inspector => ["Quick answers", "Field-ready information", "Equipment focus"],
            PersonaType.ClaimsAdjudicator => ["Neutral tone", "Template usage", "Policy references"],
            PersonaType.PolicyAnalyst => ["Multiple citations", "Detailed analysis", "Nuanced explanations"],
            _ => []
        };
    }
}

public enum PersonaType
{
    Inspector,
    ClaimsAdjudicator, 
    PolicyAnalyst,
    Administrator
}

public class PolicyMemory
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
    public int AccessCount { get; set; }

    public static PolicyMemory Create(string key, string title, string content, List<string>? tags = null, string category = "general")
    {
        return new PolicyMemory
        {
            Key = key,
            Title = title, 
            Content = content,
            Tags = tags ?? [],
            Category = category
        };
    }

    public void RecordAccess()
    {
        AccessCount++;
        LastAccessed = DateTime.UtcNow;
    }
}
