using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Configuration;

namespace OHS.Copilot.Infrastructure.Observability;

public class TelemetryService : ITelemetryService, IDisposable
{
    private readonly AppSettings _settings;
    private readonly ILogger<TelemetryService> _logger;
    private readonly Dictionary<string, ActivitySource> _activitySources = [];
    private readonly Meter _meter;
    
    private readonly Counter<int> _requestCounter;
    private readonly Histogram<double> _requestDurationHistogram;
    private readonly Counter<long> _requestSizeCounter;
    private readonly Counter<long> _responseSizeCounter;
    
    private readonly Counter<int> _agentExecutionCounter;
    private readonly Histogram<double> _agentExecutionDurationHistogram;
    private readonly Counter<int> _agentErrorCounter;
    
    private readonly Counter<int> _vectorSearchCounter;
    private readonly Histogram<double> _vectorSearchDurationHistogram;
    private readonly Counter<int> _vectorUpsertCounter;
    private readonly UpDownCounter<int> _vectorStoreSizeGauge;
    
    private readonly Counter<int> _llmRequestCounter;
    private readonly Counter<int> _llmTokenCounter;
    private readonly Histogram<double> _llmRequestDurationHistogram;
    private readonly Counter<int> _llmErrorCounter;
    
    private readonly Counter<int> _documentProcessingCounter;
    private readonly Histogram<double> _documentProcessingDurationHistogram;
    private readonly Counter<int> _documentProcessingErrorCounter;
    
    private readonly Counter<int> _memoryOperationCounter;
    private readonly Histogram<double> _memoryOperationDurationHistogram;
    private readonly UpDownCounter<long> _memorySizeGauge;
    
    private readonly Counter<int> _contentModerationCounter;
    private readonly Counter<int> _contentRedactionCounter;

    public TelemetryService(AppSettings settings, ILogger<TelemetryService> logger)
    {
        _settings = settings;
        _logger = logger;
        
        _meter = new Meter(TelemetryConstants.ServiceName, TelemetryConstants.ServiceVersion);
        
        InitializeActivitySources();
        
        // Initialize metrics
        _requestCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.RequestCount,
            description: "Total number of HTTP requests");
            
        _requestDurationHistogram = _meter.CreateHistogram<double>(
            TelemetryConstants.MetricNames.RequestDuration,
            unit: "ms",
            description: "HTTP request duration in milliseconds");
            
        _requestSizeCounter = _meter.CreateCounter<long>(
            TelemetryConstants.MetricNames.RequestSize,
            unit: "bytes",
            description: "HTTP request size in bytes");
            
        _responseSizeCounter = _meter.CreateCounter<long>(
            TelemetryConstants.MetricNames.ResponseSize,
            unit: "bytes",
            description: "HTTP response size in bytes");
        
        _agentExecutionCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.AgentExecutionCount,
            description: "Total number of agent executions");
            
        _agentExecutionDurationHistogram = _meter.CreateHistogram<double>(
            TelemetryConstants.MetricNames.AgentExecutionDuration,
            unit: "ms",
            description: "Agent execution duration in milliseconds");
            
        _agentErrorCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.AgentErrorCount,
            description: "Total number of agent errors");
        
        _vectorSearchCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.VectorSearchCount,
            description: "Total number of vector searches");
            
        _vectorSearchDurationHistogram = _meter.CreateHistogram<double>(
            TelemetryConstants.MetricNames.VectorSearchDuration,
            unit: "ms",
            description: "Vector search duration in milliseconds");
            
        _vectorUpsertCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.VectorUpsertCount,
            description: "Total number of vector upserts");
            
        _vectorStoreSizeGauge = _meter.CreateUpDownCounter<int>(
            TelemetryConstants.MetricNames.VectorStoreSize,
            description: "Current vector store size");
        
        _llmRequestCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.LlmRequestCount,
            description: "Total number of LLM requests");
            
        _llmTokenCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.LlmTokenUsage,
            description: "Total LLM token usage");
            
        _llmRequestDurationHistogram = _meter.CreateHistogram<double>(
            TelemetryConstants.MetricNames.LlmRequestDuration,
            unit: "ms",
            description: "LLM request duration in milliseconds");
            
        _llmErrorCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.LlmErrorCount,
            description: "Total number of LLM errors");
        
        _documentProcessingCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.DocumentProcessingCount,
            description: "Total number of document processing operations");
            
        _documentProcessingDurationHistogram = _meter.CreateHistogram<double>(
            TelemetryConstants.MetricNames.DocumentProcessingDuration,
            unit: "ms",
            description: "Document processing duration in milliseconds");
            
        _documentProcessingErrorCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.DocumentProcessingErrorCount,
            description: "Total number of document processing errors");
        
        _memoryOperationCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.MemoryOperationCount,
            description: "Total number of memory operations");
            
        _memoryOperationDurationHistogram = _meter.CreateHistogram<double>(
            TelemetryConstants.MetricNames.MemoryOperationDuration,
            unit: "ms",
            description: "Memory operation duration in milliseconds");
            
        _memorySizeGauge = _meter.CreateUpDownCounter<long>(
            TelemetryConstants.MetricNames.MemorySize,
            unit: "bytes",
            description: "Current memory size in bytes");
        
        _contentModerationCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.ContentModerationCount,
            description: "Total number of content moderation operations");
            
        _contentRedactionCounter = _meter.CreateCounter<int>(
            TelemetryConstants.MetricNames.ContentRedactionCount,
            description: "Total number of content redaction operations");
        
        _logger.LogInformation("TelemetryService initialized with service name: {ServiceName}", TelemetryConstants.ServiceName);
    }

    private void InitializeActivitySources()
    {
        _activitySources[TelemetryConstants.ActivitySources.AgentPipeline] = new ActivitySource(TelemetryConstants.ActivitySources.AgentPipeline);
        _activitySources[TelemetryConstants.ActivitySources.VectorStore] = new ActivitySource(TelemetryConstants.ActivitySources.VectorStore);
        _activitySources[TelemetryConstants.ActivitySources.LlmOperations] = new ActivitySource(TelemetryConstants.ActivitySources.LlmOperations);
        _activitySources[TelemetryConstants.ActivitySources.DocumentProcessing] = new ActivitySource(TelemetryConstants.ActivitySources.DocumentProcessing);
        _activitySources[TelemetryConstants.ActivitySources.Memory] = new ActivitySource(TelemetryConstants.ActivitySources.Memory);
    }


    public ActivitySource GetActivitySource(string name)
    {
        return _activitySources.TryGetValue(name, out var source) ? source : new ActivitySource(name);
    }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return GetActivitySource(TelemetryConstants.ActivitySources.AgentPipeline).StartActivity(name, kind);
    }

    public Activity? StartAgentActivity(string agentName, string operation)
    {
        return GetActivitySource(TelemetryConstants.ActivitySources.AgentPipeline)
            .StartActivity($"{TelemetryConstants.SpanNames.AgentExecution}.{agentName}")
            ?.SetAgentAttributes(agentName, operation);
    }

    public Activity? StartLlmActivity(string model, string provider)
    {
        return GetActivitySource(TelemetryConstants.ActivitySources.LlmOperations)
            .StartActivity(TelemetryConstants.SpanNames.LlmGeneration)
            ?.SetLlmAttributes(model, provider);
    }

    public Activity? StartVectorStoreActivity(string operation, string storeName)
    {
        return GetActivitySource(TelemetryConstants.ActivitySources.VectorStore)
            .StartActivity($"{TelemetryConstants.SpanNames.VectorSearch}.{operation}")
            ?.SetVectorStoreAttributes(operation, storeName);
    }

    public void RecordRequestMetrics(string method, string endpoint, int statusCode, TimeSpan duration, long requestSize, long responseSize)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(TelemetryConstants.MetricTags.Method, method),
            new(TelemetryConstants.MetricTags.Endpoint, endpoint),
            new(TelemetryConstants.MetricTags.Status, statusCode.ToString())
        };
        
        _requestCounter.Add(1, tags);
        _requestDurationHistogram.Record(duration.TotalMilliseconds, tags);
        _requestSizeCounter.Add(requestSize, tags);
        _responseSizeCounter.Add(responseSize, tags);
        
        _logger.LogDebug("Recorded request metrics: {Method} {Endpoint} {Status} {Duration}ms", 
            method, endpoint, statusCode, duration.TotalMilliseconds);
    }

    public void RecordAgentMetrics(string agentName, TimeSpan duration, bool success)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(TelemetryConstants.MetricTags.Agent, agentName),
            new(TelemetryConstants.MetricTags.Status, success ? "success" : "failure")
        };
        
        _agentExecutionCounter.Add(1, tags);
        _agentExecutionDurationHistogram.Record(duration.TotalMilliseconds, tags);
        
        if (!success)
        {
            _agentErrorCounter.Add(1, new KeyValuePair<string, object?>[]
            {
                new(TelemetryConstants.MetricTags.Agent, agentName)
            });
        }
    }

    public void RecordVectorStoreMetrics(string operation, string storeName, int count, TimeSpan duration)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(TelemetryConstants.MetricTags.Operation, operation),
            new(TelemetryConstants.MetricTags.VectorStore, storeName)
        };
        
        if (operation == "search")
        {
            _vectorSearchCounter.Add(1, tags);
            _vectorSearchDurationHistogram.Record(duration.TotalMilliseconds, tags);
        }
        else if (operation == "upsert")
        {
            _vectorUpsertCounter.Add(count, tags);
            _vectorStoreSizeGauge.Add(count, tags);
        }
    }

    public void RecordLlmMetrics(string model, string provider, int promptTokens, int completionTokens, TimeSpan duration)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(TelemetryConstants.MetricTags.Model, model),
            new(TelemetryConstants.MetricTags.Provider, provider)
        };
        
        _llmRequestCounter.Add(1, tags);
        _llmTokenCounter.Add(promptTokens + completionTokens, tags);
        _llmRequestDurationHistogram.Record(duration.TotalMilliseconds, tags);
    }

    public void RecordDocumentProcessingMetrics(string documentType, int chunkCount, TimeSpan duration, bool success)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(TelemetryConstants.MetricTags.DocumentType, documentType),
            new(TelemetryConstants.MetricTags.Status, success ? "success" : "failure")
        };
        
        _documentProcessingCounter.Add(1, tags);
        _documentProcessingDurationHistogram.Record(duration.TotalMilliseconds, tags);
        
        if (!success)
        {
            _documentProcessingErrorCounter.Add(1, new KeyValuePair<string, object?>[]
            {
                new(TelemetryConstants.MetricTags.DocumentType, documentType)
            });
        }
    }

    public void RecordMemoryMetrics(string memoryType, string operation, TimeSpan duration, long sizeBytes)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(TelemetryConstants.MetricTags.MemoryType, memoryType),
            new(TelemetryConstants.MetricTags.Operation, operation)
        };
        
        _memoryOperationCounter.Add(1, tags);
        _memoryOperationDurationHistogram.Record(duration.TotalMilliseconds, tags);
        _memorySizeGauge.Add(sizeBytes, new KeyValuePair<string, object?>[]
        {
            new(TelemetryConstants.MetricTags.MemoryType, memoryType)
        });
    }

    public void RecordContentModerationMetrics(string[] categories, int severity)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(TelemetryConstants.MetricTags.Category, string.Join(",", categories)),
            new("severity", severity.ToString())
        };
        
        _contentModerationCounter.Add(1, tags);
    }

    public void RecordContentRedactionMetrics(int redactionCount, TimeSpan duration)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("redaction_count", redactionCount.ToString())
        };
        
        _contentRedactionCounter.Add(1, tags);
    }

    public void IncrementCounter(string name, int value = 1, params KeyValuePair<string, object?>[] tags)
    {
        try
        {
            var counter = _meter.CreateCounter<int>(name);
            counter.Add(value, tags);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment counter {CounterName}", name);
        }
    }

    public void RecordValue(string name, double value, params KeyValuePair<string, object?>[] tags)
    {
        try
        {
            var histogram = _meter.CreateHistogram<double>(name);
            histogram.Record(value, tags);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record value for {MetricName}", name);
        }
    }

    public void Dispose()
    {
        foreach (var source in _activitySources.Values)
        {
            source.Dispose();
        }
        
        _meter.Dispose();
        _logger.LogInformation("TelemetryService disposed");
    }
}
