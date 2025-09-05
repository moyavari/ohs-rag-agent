namespace OHS.Copilot.Application.Interfaces;

public interface IAgent
{
    string Name { get; }
    Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default);
}

public class AgentContext
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public string? ConversationId { get; set; }
    public Dictionary<string, object> Data { get; set; } = [];
    public List<AgentTrace> Traces { get; set; } = [];

    public void AddTrace(string agentName, string action, object? data = null, TimeSpan? duration = null)
    {
        Traces.Add(new AgentTrace
        {
            AgentName = agentName,
            Action = action,
            Data = data,
            Duration = duration ?? TimeSpan.Zero,
            Timestamp = DateTime.UtcNow
        });
    }

    public T? GetData<T>(string key) where T : class
    {
        return Data.TryGetValue(key, out var value) && value is T typedValue ? typedValue : null;
    }

    public void SetData<T>(string key, T value) where T : class
    {
        Data[key] = value;
    }
}

public class AgentResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Data { get; set; } = [];
    public TimeSpan ExecutionTime { get; set; }

    public static AgentResult Successful(Dictionary<string, object>? data = null)
    {
        return new AgentResult
        {
            Success = true,
            Data = data ?? []
        };
    }

    public static AgentResult Failed(string errorMessage)
    {
        return new AgentResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

public class AgentTrace
{
    public string AgentName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public object? Data { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; }
}
