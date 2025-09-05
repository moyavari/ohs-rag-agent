namespace OHS.Copilot.Domain.Entities;

public class AuditLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string PromptSha { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public Dictionary<string, object> Inputs { get; set; } = [];
    public Dictionary<string, object> Outputs { get; set; } = [];
    public List<string> CitationIds { get; set; } = [];
    public Dictionary<string, object> AgentTraces { get; set; } = [];
    public Dictionary<string, object> ModerationResult { get; set; } = [];
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public TimeSpan Duration { get; set; }

    public static AuditLogEntry Create(
        string operation, 
        string promptSha, 
        string model, 
        string? userId = null)
    {
        return new AuditLogEntry
        {
            Operation = operation,
            PromptSha = promptSha,
            Model = model,
            UserId = userId
        };
    }

    public void AddAgentTrace(string agentName, string tool, object args, TimeSpan duration)
    {
        if (!AgentTraces.ContainsKey("traces"))
        {
            AgentTraces["traces"] = new List<object>();
        }

        var traces = (List<object>)AgentTraces["traces"];
        traces.Add(new
        {
            agent = agentName,
            tool,
            args,
            duration = duration.TotalMilliseconds
        });
    }

    public void SetModerationResult(string category, string severity, string action)
    {
        ModerationResult = new Dictionary<string, object>
        {
            ["category"] = category,
            ["severity"] = severity,
            ["action"] = action,
            ["timestamp"] = DateTime.UtcNow
        };
    }
}
