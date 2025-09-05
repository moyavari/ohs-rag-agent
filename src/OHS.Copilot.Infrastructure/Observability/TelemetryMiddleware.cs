using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;

namespace OHS.Copilot.Infrastructure.Observability;

public class TelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<TelemetryMiddleware> _logger;

    public TelemetryMiddleware(RequestDelegate next, ITelemetryService telemetryService, ILogger<TelemetryMiddleware> logger)
    {
        _next = next;
        _telemetryService = telemetryService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestSize = GetRequestSize(context.Request);
        
        using var activity = _telemetryService.StartActivity("http.request", ActivityKind.Server);
        
        activity?
            .SetTag("http.method", context.Request.Method)
            .SetTag("http.url", context.Request.GetDisplayUrl())
            .SetTag("http.scheme", context.Request.Scheme)
            .SetTag("http.host", context.Request.Host.Value)
            .SetTag("http.target", context.Request.Path)
            .SetTag("http.request_content_length", requestSize)
            .SetTag("user_agent.original", context.Request.Headers.UserAgent.ToString());
        
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;
        
        Exception? exception = null;
        
        try
        {
            await _next(context);
            
            stopwatch.Stop();
            
            var responseSize = responseBodyStream.Length;
            
            activity?
                .SetTag("http.status_code", context.Response.StatusCode)
                .SetTag("http.response_content_length", responseSize)
                .SetTag("http.response.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            _telemetryService.RecordRequestMetrics(
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                stopwatch.Elapsed,
                requestSize,
                responseSize);
            
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);
            
            _logger.LogDebug(
                "Request completed: {Method} {Path} {StatusCode} in {Duration}ms (Request: {RequestSize}B, Response: {ResponseSize}B)",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                requestSize,
                responseSize);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            exception = ex;
            activity?.SetErrorAttributes(ex);
            
            var responseSize = responseBodyStream.Length;
            
            _telemetryService.RecordRequestMetrics(
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                stopwatch.Elapsed,
                requestSize,
                responseSize);
            
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static long GetRequestSize(HttpRequest request)
    {
        return request.ContentLength ?? 0;
    }
}

public static class HttpRequestExtensions
{
    public static string GetDisplayUrl(this HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}";
    }
}
