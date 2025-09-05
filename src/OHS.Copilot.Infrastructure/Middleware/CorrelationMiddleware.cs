using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OHS.Copilot.Infrastructure.Middleware;

public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        using (_logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            context.Response.Headers.Append("X-Correlation-ID", correlationId);
            
            _logger.LogInformation("Request started: {Method} {Path}", 
                context.Request.Method, 
                context.Request.Path);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                await _next(context);
                
                stopwatch.Stop();
                
                _logger.LogInformation("Request completed: {Method} {Path} - {StatusCode} in {Duration}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _logger.LogError(ex, "Request failed: {Method} {Path} - {StatusCode} in {Duration}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
                
                throw;
            }
        }
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items["CorrelationId"] = correlationId;
        return correlationId;
    }
}
