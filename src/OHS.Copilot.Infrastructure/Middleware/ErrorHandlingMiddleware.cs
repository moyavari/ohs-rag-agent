using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace OHS.Copilot.Infrastructure.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
        
        var (statusCode, title, detail) = exception switch
        {
            ValidationException validationEx => (400, "Validation Failed", validationEx.Message),
            ArgumentException argumentEx => (400, "Invalid Request", argumentEx.Message),
            NotSupportedException notSupportedEx => (400, "Operation Not Supported", notSupportedEx.Message),
            UnauthorizedAccessException => (401, "Unauthorized", "Authentication required"),
            InvalidOperationException invalidOpEx => (409, "Invalid Operation", invalidOpEx.Message),
            TimeoutException => (408, "Request Timeout", "The operation timed out"),
            NotImplementedException => (501, "Not Implemented", "This feature is not yet implemented"),
            _ => (500, "Internal Server Error", "An unexpected error occurred")
        };

        var response = new ErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Title = title,
            Status = statusCode,
            Detail = detail,
            TraceId = System.Diagnostics.Activity.Current?.Id,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };

        if (statusCode >= 500)
        {
            response.Detail = "An internal server error occurred. Please try again later.";
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(response, options);
        await context.Response.WriteAsync(json);
    }
}

public class ErrorResponse
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? TraceId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
