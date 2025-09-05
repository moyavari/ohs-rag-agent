using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OHS.Copilot.Infrastructure.Configuration;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var appSettings = new AppSettings();
        BindFromEnvironment(appSettings, configuration);
        appSettings.Validate();

        services.AddSingleton(appSettings);
        return services;
    }

    private static void BindFromEnvironment(AppSettings settings, IConfiguration configuration)
    {
        settings.DemoMode = GetBoolValue(configuration, "DEMO_MODE", false);

        BindAzureOpenAI(settings.AzureOpenAI, configuration);
        BindVectorStore(settings.VectorStore, configuration);
        BindQdrant(settings.Qdrant, configuration);
        BindPostgreSQL(settings.PostgreSQL, configuration);
        BindCosmosDb(settings.CosmosDb, configuration);
        BindMemory(settings.Memory, configuration);
        BindContentSafety(settings.ContentSafety, configuration);
        BindApplicationInsights(settings.ApplicationInsights, configuration);
        BindLogging(settings.Logging, configuration);
        BindSecurity(settings.Security, configuration);
        BindGovernance(settings.Governance, configuration);
        BindPerformance(settings.Performance, configuration);
        BindDemoMode(settings.Demo, configuration);
    }

    private static void BindAzureOpenAI(AzureOpenAISettings settings, IConfiguration configuration)
    {
        settings.Endpoint = GetStringValue(configuration, "AOAI_ENDPOINT");
        settings.ApiKey = GetStringValue(configuration, "AOAI_API_KEY");
        settings.ChatDeployment = GetStringValue(configuration, "AOAI_CHAT_DEPLOYMENT", "gpt-4");
        settings.EmbeddingDeployment = GetStringValue(configuration, "AOAI_EMB_DEPLOYMENT", "text-embedding-ada-002");
    }

    private static void BindVectorStore(VectorStoreSettings settings, IConfiguration configuration)
    {
        settings.Type = GetStringValue(configuration, "VECTOR_STORE", "qdrant");
    }

    private static void BindQdrant(QdrantSettings settings, IConfiguration configuration)
    {
        settings.Endpoint = GetStringValue(configuration, "QDRANT_ENDPOINT", "http://localhost:6333");
        settings.ApiKey = GetStringValue(configuration, "QDRANT_API_KEY");
    }

    private static void BindPostgreSQL(PostgreSQLSettings settings, IConfiguration configuration)
    {
        settings.ConnectionString = GetStringValue(configuration, "PG_CONN_STR");
    }

    private static void BindCosmosDb(CosmosDbSettings settings, IConfiguration configuration)
    {
        settings.ConnectionString = GetStringValue(configuration, "COSMOS_CONN_STR");
    }

    private static void BindMemory(MemorySettings settings, IConfiguration configuration)
    {
        settings.Backend = GetStringValue(configuration, "MEMORY_BACKEND", "cosmos");
        settings.CosmosConnectionString = GetStringValue(configuration, "MEMORY_COSMOS_CONN_STR");
        settings.PostgreSQLConnectionString = GetStringValue(configuration, "MEMORY_PG_CONN_STR");
    }

    private static void BindContentSafety(ContentSafetySettings settings, IConfiguration configuration)
    {
        settings.Endpoint = GetStringValue(configuration, "CONTENT_SAFETY_ENDPOINT");
        settings.Key = GetStringValue(configuration, "CONTENT_SAFETY_KEY");
        settings.Threshold = GetStringValue(configuration, "CONTENT_SAFETY_THRESHOLD", "Medium");
        settings.RedactionEnabled = GetBoolValue(configuration, "REDACTION_ENABLED", true);
    }

    private static void BindApplicationInsights(ApplicationInsightsSettings settings, IConfiguration configuration)
    {
        settings.ConnectionString = GetStringValue(configuration, "APPLICATIONINSIGHTS_CONNECTION_STRING");
    }

    private static void BindLogging(LoggingSettings settings, IConfiguration configuration)
    {
        settings.Sampling = GetDoubleValue(configuration, "LOG_SAMPLING", 0.1);
    }

    private static void BindSecurity(SecuritySettings settings, IConfiguration configuration)
    {
        settings.JwtSecret = GetStringValue(configuration, "JWT_SECRET");
        settings.JwtIssuer = GetStringValue(configuration, "JWT_ISSUER", "OHS.Copilot");
        settings.JwtAudience = GetStringValue(configuration, "JWT_AUDIENCE", "OHS.Copilot.API");
    }

    private static void BindGovernance(GovernanceSettings settings, IConfiguration configuration)
    {
        settings.PromptVersionStore = GetStringValue(configuration, "PROMPT_VERSION_STORE", "memory");
        settings.AuditLogRetentionDays = GetIntValue(configuration, "AUDIT_LOG_RETENTION_DAYS", 30);
    }

    private static void BindPerformance(PerformanceSettings settings, IConfiguration configuration)
    {
        settings.MaxTokensPerRequest = GetIntValue(configuration, "MAX_TOKENS_PER_REQUEST", 4096);
        settings.MaxConcurrentRequests = GetIntValue(configuration, "MAX_CONCURRENT_REQUESTS", 10);
        settings.VectorSearchTopK = GetIntValue(configuration, "VECTOR_SEARCH_TOP_K", 10);
        settings.RerankEnabled = GetBoolValue(configuration, "RERANK_ENABLED", false);
    }

    private static void BindDemoMode(DemoModeSettings settings, IConfiguration configuration)
    {
        settings.FixturesPath = GetStringValue(configuration, "DEMO_FIXTURES_PATH", "./fixtures");
        settings.TracePath = GetStringValue(configuration, "DEMO_TRACE_PATH", "./docs/traces");
    }

    private static string GetStringValue(IConfiguration configuration, string key, string defaultValue = "")
    {
        return configuration[key] ?? defaultValue;
    }

    private static bool GetBoolValue(IConfiguration configuration, string key, bool defaultValue = false)
    {
        var value = configuration[key];
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    private static int GetIntValue(IConfiguration configuration, string key, int defaultValue = 0)
    {
        var value = configuration[key];
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static double GetDoubleValue(IConfiguration configuration, string key, double defaultValue = 0.0)
    {
        var value = configuration[key];
        return double.TryParse(value, out var result) ? result : defaultValue;
    }
}
