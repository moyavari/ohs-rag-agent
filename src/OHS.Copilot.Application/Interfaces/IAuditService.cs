using OHS.Copilot.Domain.Entities;

namespace OHS.Copilot.Application.Interfaces;

public interface IAuditService
{
    Task<string> LogRequestAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task UpdateRequestAsync(string auditId, Dictionary<string, object> outputs, List<string> citationIds, CancellationToken cancellationToken = default);
    Task AddAgentTraceAsync(string auditId, string agentName, string tool, object args, TimeSpan duration, CancellationToken cancellationToken = default);
    Task SetModerationResultAsync(string auditId, Dictionary<string, object> moderationResult, CancellationToken cancellationToken = default);
    Task SetTokenUsageAsync(string auditId, int? inputTokens, int? outputTokens, CancellationToken cancellationToken = default);
    Task<AuditLogEntry?> GetAuditLogAsync(string auditId, CancellationToken cancellationToken = default);
    Task<List<AuditLogEntry>> GetAuditLogsByUserAsync(string userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<long> GetAuditLogCountAsync(CancellationToken cancellationToken = default);
    Task CleanupExpiredLogsAsync(TimeSpan retention, CancellationToken cancellationToken = default);
}

public class AuditContext
{
    public string CorrelationId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptSha { get; set; } = string.Empty;
    public Dictionary<string, object> Inputs { get; set; } = [];
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public static AuditContext Create(string correlationId, string operation, string model, string promptSha, string? userId = null)
    {
        return new AuditContext
        {
            CorrelationId = correlationId,
            Operation = operation,
            Model = model,
            PromptSha = promptSha,
            UserId = userId
        };
    }
}
