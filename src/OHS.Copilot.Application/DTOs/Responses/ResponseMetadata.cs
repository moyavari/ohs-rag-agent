namespace OHS.Copilot.Application.DTOs.Responses;

public class ResponseMetadata
{
    public string PromptSha { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public string VectorStore { get; set; } = string.Empty;
    public int RetrievedChunks { get; set; }
    public Dictionary<string, object> AgentTraces { get; set; } = [];
    public Dictionary<string, object> ModerationResult { get; set; } = [];

    public void AddAgentTrace(string agentName, string action, TimeSpan duration)
    {
        if (!AgentTraces.ContainsKey("traces"))
        {
            AgentTraces["traces"] = new List<object>();
        }

        var traces = (List<object>)AgentTraces["traces"];
        traces.Add(new
        {
            agent = agentName,
            action,
            duration = duration.TotalMilliseconds
        });
    }
}
