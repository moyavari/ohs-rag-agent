namespace OHS.Copilot.Application.DTOs.Responses;

public class MetricsResponse
{
    public int TotalRequests { get; set; }
    public int TotalTokensUsed { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public Dictionary<string, int> RequestsByEndpoint { get; set; } = [];
    public Dictionary<string, double> AverageLatencyByEndpoint { get; set; } = [];
    public Dictionary<string, int> ErrorsByType { get; set; } = [];
    public DateTime LastResetTime { get; set; }
    public string Format { get; set; } = "json";

    public void Reset()
    {
        TotalRequests = 0;
        TotalTokensUsed = 0;
        InputTokens = 0;
        OutputTokens = 0;
        RequestsByEndpoint.Clear();
        AverageLatencyByEndpoint.Clear();
        ErrorsByType.Clear();
        LastResetTime = DateTime.UtcNow;
    }
}
