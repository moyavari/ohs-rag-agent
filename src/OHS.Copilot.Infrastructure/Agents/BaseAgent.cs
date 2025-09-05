using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using System.Diagnostics;

namespace OHS.Copilot.Infrastructure.Agents;

public abstract class BaseAgent : IAgent
{
    protected readonly Kernel Kernel;
    protected readonly ILogger Logger;
    protected readonly ITelemetryService? TelemetryService;

    public abstract string Name { get; }

    protected BaseAgent(Kernel kernel, ILogger logger, ITelemetryService? telemetryService = null)
    {
        Kernel = kernel;
        Logger = logger;
        TelemetryService = telemetryService;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = TelemetryService?.StartAgentActivity(Name, "execute");
        activity?.SetConversationAttributes(context.ConversationId);
        
        try
        {
            Logger.LogDebug("Agent {AgentName} starting execution for correlation {CorrelationId}", Name, context.CorrelationId);
            
            var result = await ExecuteInternalAsync(context, cancellationToken);
            
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
            
            context.AddTrace(Name, "execute", new { success = result.Success }, stopwatch.Elapsed);
            
            activity?
                .SetTag("agent.status", result.Success ? "success" : "failure")
                .SetTag("agent.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            
            TelemetryService?.RecordAgentMetrics(Name, stopwatch.Elapsed, result.Success);
            
            if (result.Success)
            {
                Logger.LogDebug("Agent {AgentName} completed successfully in {Duration}ms", Name, stopwatch.ElapsedMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                Logger.LogWarning("Agent {AgentName} failed: {ErrorMessage}", Name, result.ErrorMessage);
                activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Agent {AgentName} threw exception", Name);
            
            context.AddTrace(Name, "error", new { error = ex.Message }, stopwatch.Elapsed);
            
            activity?.SetErrorAttributes(ex);
            TelemetryService?.RecordAgentMetrics(Name, stopwatch.Elapsed, false);
            
            return AgentResult.Failed($"Agent {Name} failed: {ex.Message}");
        }
    }

    protected abstract Task<AgentResult> ExecuteInternalAsync(AgentContext context, CancellationToken cancellationToken);

    protected void LogAction(string action, object? data = null)
    {
        Logger.LogDebug("Agent {AgentName} - {Action}: {Data}", Name, action, data);
    }

    protected static string ComputePromptHash(string prompt)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(prompt);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    protected KernelArguments CreateKernelArguments(Dictionary<string, object>? parameters = null)
    {
        var args = new KernelArguments();
        
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                args[kvp.Key] = kvp.Value;
            }
        }
        
        return args;
    }
}
