namespace OHS.Copilot.Application.DTOs.Responses;

public class HealthResponse
{
    public bool Ok { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, ComponentHealth> Dependencies { get; set; } = [];
    public string Version { get; set; } = string.Empty;

    public static HealthResponse Healthy(string version)
    {
        return new HealthResponse
        {
            Ok = true,
            Status = "Healthy",
            Version = version
        };
    }

    public static HealthResponse Unhealthy(string reason, string version)
    {
        return new HealthResponse
        {
            Ok = false,
            Status = $"Unhealthy: {reason}",
            Version = version
        };
    }
}

public class ComponentHealth
{
    public bool Healthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }
}
