namespace OHS.Copilot.Application.DTOs.Requests;

public class EvaluationRequest
{
    public bool RunDemoMode { get; set; } = false;
    public bool RunLiveMode { get; set; } = false;
    public int MaxConcurrentRequests { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 30;
    public string? ReportOutputPath { get; set; }
}
