using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.Entities;
using System.Collections.Concurrent;

namespace OHS.Copilot.Infrastructure.Services;

public class InMemoryAuditService : IAuditService
{
    private readonly ConcurrentDictionary<string, AuditLogEntry> _auditLogs = new();
    private readonly ILogger<InMemoryAuditService> _logger;

    public InMemoryAuditService(ILogger<InMemoryAuditService> logger)
    {
        _logger = logger;
    }

    public Task<string> LogRequestAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        _auditLogs[entry.Id] = entry;
        
        _logger.LogInformation("Audit log created: {AuditId} for operation {Operation} by user {UserId}",
            entry.Id, entry.Operation, entry.UserId ?? "anonymous");

        return Task.FromResult(entry.Id);
    }

    public Task UpdateRequestAsync(string auditId, Dictionary<string, object> outputs, List<string> citationIds, CancellationToken cancellationToken = default)
    {
        if (_auditLogs.TryGetValue(auditId, out var entry))
        {
            entry.Outputs = outputs;
            entry.CitationIds = citationIds;
            entry.Duration = DateTime.UtcNow - entry.Timestamp;
            
            _logger.LogDebug("Updated audit log {AuditId} with outputs and citations", auditId);
        }
        else
        {
            _logger.LogWarning("Audit log {AuditId} not found for update", auditId);
        }

        return Task.CompletedTask;
    }

    public Task AddAgentTraceAsync(string auditId, string agentName, string tool, object args, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (_auditLogs.TryGetValue(auditId, out var entry))
        {
            entry.AddAgentTrace(agentName, tool, args, duration);
            
            _logger.LogDebug("Added agent trace for {AgentName} to audit log {AuditId}", agentName, auditId);
        }
        else
        {
            _logger.LogWarning("Audit log {AuditId} not found for agent trace", auditId);
        }

        return Task.CompletedTask;
    }

    public Task SetModerationResultAsync(string auditId, Dictionary<string, object> moderationResult, CancellationToken cancellationToken = default)
    {
        if (_auditLogs.TryGetValue(auditId, out var entry))
        {
            entry.ModerationResult = moderationResult;
            
            _logger.LogDebug("Added moderation result to audit log {AuditId}", auditId);
        }
        else
        {
            _logger.LogWarning("Audit log {AuditId} not found for moderation result", auditId);
        }

        return Task.CompletedTask;
    }

    public Task SetTokenUsageAsync(string auditId, int? inputTokens, int? outputTokens, CancellationToken cancellationToken = default)
    {
        if (_auditLogs.TryGetValue(auditId, out var entry))
        {
            entry.InputTokens = inputTokens;
            entry.OutputTokens = outputTokens;
            
            _logger.LogDebug("Updated token usage for audit log {AuditId}: Input={InputTokens}, Output={OutputTokens}",
                auditId, inputTokens, outputTokens);
        }
        else
        {
            _logger.LogWarning("Audit log {AuditId} not found for token usage update", auditId);
        }

        return Task.CompletedTask;
    }

    public Task<AuditLogEntry?> GetAuditLogAsync(string auditId, CancellationToken cancellationToken = default)
    {
        _auditLogs.TryGetValue(auditId, out var entry);
        return Task.FromResult(entry);
    }

    public Task<List<AuditLogEntry>> GetAuditLogsByUserAsync(string userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var logs = _auditLogs.Values
            .Where(log => log.UserId == userId)
            .Where(log => from == null || log.Timestamp >= from)
            .Where(log => to == null || log.Timestamp <= to)
            .OrderByDescending(log => log.Timestamp)
            .ToList();

        _logger.LogDebug("Retrieved {Count} audit logs for user {UserId}", logs.Count, userId);

        return Task.FromResult(logs);
    }

    public Task<long> GetAuditLogCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((long)_auditLogs.Count);
    }

    public Task CleanupExpiredLogsAsync(TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow - retention;
        var expiredKeys = _auditLogs
            .Where(kvp => kvp.Value.Timestamp < cutoffDate)
            .Select(kvp => kvp.Key)
            .ToList();

        var removedCount = 0;
        foreach (var key in expiredKeys)
        {
            if (_auditLogs.TryRemove(key, out _))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired audit logs older than {Retention}", 
                removedCount, retention);
        }

        return Task.CompletedTask;
    }

    public void ExportAuditLogs(string filePath)
    {
        try
        {
            var logs = _auditLogs.Values.OrderBy(log => log.Timestamp).ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(logs, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            
            File.WriteAllText(filePath, json);
            
            _logger.LogInformation("Exported {Count} audit logs to {FilePath}", logs.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export audit logs to {FilePath}", filePath);
        }
    }
}
