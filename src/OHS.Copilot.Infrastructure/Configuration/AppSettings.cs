using System.ComponentModel.DataAnnotations;
using OHS.Copilot.Infrastructure.Observability;

namespace OHS.Copilot.Infrastructure.Configuration;

public class AppSettings
{
    public bool DemoMode { get; set; } = false;

    [Required]
    public AzureOpenAISettings AzureOpenAI { get; set; } = new();

    [Required]
    public VectorStoreSettings VectorStore { get; set; } = new();

    public QdrantSettings Qdrant { get; set; } = new();
    public PostgreSQLSettings PostgreSQL { get; set; } = new();
    public CosmosDbSettings CosmosDb { get; set; } = new();

    [Required]
    public MemorySettings Memory { get; set; } = new();
    public TelemetrySettings Telemetry { get; set; } = new();
    public ApplicationInsightsSettings ApplicationInsights { get; set; } = new();
    public PrometheusSettings Prometheus { get; set; } = new();
    public JaegerSettings Jaeger { get; set; } = new();

    public ContentSafetySettings ContentSafety { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public GovernanceSettings Governance { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public DemoModeSettings Demo { get; set; } = new();

    public void Validate()
    {
        var context = new ValidationContext(this);
        Validator.ValidateObject(this, context, true);
        
        AzureOpenAI.Validate();
        VectorStore.Validate();
        Memory.Validate();
    }
}

public class AzureOpenAISettings
{
    [Required, Url]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string ChatDeployment { get; set; } = string.Empty;

    [Required]
    public string EmbeddingDeployment { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new InvalidOperationException("Azure OpenAI Endpoint is required");
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("Azure OpenAI API Key is required");
    }
}

public class VectorStoreSettings
{
    [Required]
    public string Type { get; set; } = "qdrant";

    public void Validate()
    {
        var validTypes = new[] { "json", "qdrant", "pgvector", "cosmos" };
        if (!validTypes.Contains(Type.ToLower()))
            throw new InvalidOperationException($"Vector store type must be one of: {string.Join(", ", validTypes)}");
    }
}

public class QdrantSettings
{
    [Url]
    public string Endpoint { get; set; } = "http://localhost:6333";
    public string? ApiKey { get; set; }
}

public class PostgreSQLSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class CosmosDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class MemorySettings
{
    [Required]
    public string Backend { get; set; } = "cosmos";
    public string CosmosConnectionString { get; set; } = string.Empty;
    public string PostgreSQLConnectionString { get; set; } = string.Empty;

    public void Validate()
    {
        var validBackends = new[] { "cosmos", "pg" };
        if (!validBackends.Contains(Backend.ToLower()))
            throw new InvalidOperationException($"Memory backend must be one of: {string.Join(", ", validBackends)}");
    }
}

public class ContentSafetySettings
{
    [Url]
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Threshold { get; set; } = "Medium";
    public bool RedactionEnabled { get; set; } = true;
}

public class ApplicationInsightsSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class LoggingSettings
{
    public double Sampling { get; set; } = 0.1;
}

public class SecuritySettings
{
    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "OHS.Copilot";
    public string JwtAudience { get; set; } = "OHS.Copilot.API";
}

public class GovernanceSettings
{
    public string PromptVersionStore { get; set; } = "memory";
    public int AuditLogRetentionDays { get; set; } = 30;
}

public class PerformanceSettings
{
    public int MaxTokensPerRequest { get; set; } = 4096;
    public int MaxConcurrentRequests { get; set; } = 10;
    public int VectorSearchTopK { get; set; } = 10;
    public bool RerankEnabled { get; set; } = false;
}

public class DemoModeSettings
{
    public string FixturesPath { get; set; } = "./fixtures";
    public string TracePath { get; set; } = "./docs/traces";
}
