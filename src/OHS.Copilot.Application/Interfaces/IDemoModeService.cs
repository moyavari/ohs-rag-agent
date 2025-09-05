using OHS.Copilot.Application.DTOs.Requests;
using OHS.Copilot.Application.DTOs.Responses;

namespace OHS.Copilot.Application.Interfaces;

public interface IDemoModeService
{
    Task<AskResponse?> GetDemoAskResponseAsync(AskRequest request, CancellationToken cancellationToken = default);
    Task<DraftLetterResponse?> GetDemoLetterResponseAsync(DraftLetterRequest request, CancellationToken cancellationToken = default);
    Task<List<DemoFixture>> LoadFixturesAsync(CancellationToken cancellationToken = default);
    Task<DemoTrace?> GetDemoTraceAsync(string traceId, CancellationToken cancellationToken = default);
    bool IsDemoModeEnabled();
}

public class DemoFixture
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "ask" or "draft"
    public object Request { get; set; } = new();
    public object Response { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static DemoFixture CreateAskFixture(string id, AskRequest request, AskResponse response)
    {
        return new DemoFixture
        {
            Id = id,
            Type = "ask",
            Request = request,
            Response = response,
            Metadata = new Dictionary<string, object>
            {
                ["promptSha"] = response.Metadata.PromptSha,
                ["processingTime"] = response.Metadata.ProcessingTime,
                ["agentCount"] = response.Metadata.AgentTraces.ContainsKey("total_agents") ? response.Metadata.AgentTraces["total_agents"] : 0
            }
        };
    }

    public static DemoFixture CreateLetterFixture(string id, DraftLetterRequest request, DraftLetterResponse response)
    {
        return new DemoFixture
        {
            Id = id,
            Type = "draft",
            Request = request,
            Response = response,
            Metadata = new Dictionary<string, object>
            {
                ["promptSha"] = response.Metadata.PromptSha,
                ["processingTime"] = response.Metadata.ProcessingTime,
                ["placeholderCount"] = response.Placeholders.Count
            }
        };
    }
}

public class DemoTrace
{
    public string TraceId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public List<DemoSpan> Spans { get; set; } = [];

    public static DemoTrace Create(string traceId, string operation, List<DemoSpan> spans)
    {
        return new DemoTrace
        {
            TraceId = traceId,
            Operation = operation,
            Spans = spans,
            Duration = TimeSpan.FromMilliseconds(spans.Sum(s => s.Duration.TotalMilliseconds))
        };
    }
}

public class DemoSpan
{
    public string SpanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> Tags { get; set; } = [];
    public List<DemoSpan> Children { get; set; } = [];
}
