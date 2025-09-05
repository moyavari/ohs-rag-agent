namespace OHS.Copilot.Infrastructure.Observability;

public static class TelemetryConstants
{
    public const string ServiceName = "OHS.Copilot.API";
    public const string ServiceVersion = "1.0.0";

    public static class ActivitySources
    {
        public const string AgentPipeline = "OHS.Copilot.Agents";
        public const string VectorStore = "OHS.Copilot.VectorStore";
        public const string LlmOperations = "OHS.Copilot.LLM";
        public const string DocumentProcessing = "OHS.Copilot.Documents";
        public const string Memory = "OHS.Copilot.Memory";
    }

    public static class SpanNames
    {
        public const string AgentExecution = "agent.execute";
        public const string VectorSearch = "vector.search";
        public const string VectorUpsert = "vector.upsert";
        public const string LlmGeneration = "llm.generation";
        public const string EmbeddingGeneration = "embedding.generation";
        public const string DocumentParsing = "document.parse";
        public const string TextChunking = "text.chunk";
        public const string MemoryRetrieval = "memory.retrieve";
        public const string MemoryUpdate = "memory.update";
        public const string ContentModeration = "content.moderate";
        public const string ContentRedaction = "content.redact";
    }

    public static class SpanAttributes
    {
        public const string AgentName = "agent.name";
        public const string AgentType = "agent.type";
        public const string AgentDuration = "agent.duration_ms";
        public const string AgentStatus = "agent.status";
        
        public const string VectorStoreName = "vector_store.name";
        public const string VectorStoreOperation = "vector_store.operation";
        public const string VectorCount = "vector_store.count";
        public const string VectorDimensions = "vector_store.dimensions";
        public const string VectorSimilarityThreshold = "vector_store.similarity_threshold";
        
        public const string LlmModel = "llm.model";
        public const string LlmProvider = "llm.provider";
        public const string LlmTokensPrompt = "llm.tokens.prompt";
        public const string LlmTokensCompletion = "llm.tokens.completion";
        public const string LlmTokensTotal = "llm.tokens.total";
        public const string LlmTemperature = "llm.temperature";
        public const string LlmMaxTokens = "llm.max_tokens";
        public const string LlmFinishReason = "llm.finish_reason";
        
        public const string DocumentPath = "document.path";
        public const string DocumentType = "document.type";
        public const string DocumentSize = "document.size_bytes";
        public const string DocumentPageCount = "document.page_count";
        public const string ChunkCount = "document.chunk_count";
        public const string ChunkSize = "document.chunk_size";
        
        public const string MemoryType = "memory.type";
        public const string MemoryKey = "memory.key";
        public const string MemoryTtl = "memory.ttl_seconds";
        public const string ConversationId = "conversation.id";
        public const string ConversationTurnCount = "conversation.turn_count";
        
        public const string ContentLength = "content.length";
        public const string ContentLanguage = "content.language";
        public const string ContentRedactionCount = "content.redaction_count";
        public const string ContentModerationCategories = "content.moderation_categories";
        public const string ContentModerationSeverity = "content.moderation_severity";
        
        public const string ErrorType = "error.type";
        public const string ErrorMessage = "error.message";
        public const string ErrorStackTrace = "error.stack_trace";
        
        public const string RequestId = "request.id";
        public const string UserId = "user.id";
        public const string SessionId = "session.id";
    }

    public static class MetricNames
    {
        public const string RequestDuration = "http_request_duration";
        public const string RequestCount = "http_request_total";
        public const string RequestSize = "http_request_size_bytes";
        public const string ResponseSize = "http_response_size_bytes";
        
        public const string AgentExecutionDuration = "agent_execution_duration";
        public const string AgentExecutionCount = "agent_execution_total";
        public const string AgentErrorCount = "agent_error_total";
        
        public const string VectorSearchDuration = "vector_search_duration";
        public const string VectorSearchCount = "vector_search_total";
        public const string VectorUpsertCount = "vector_upsert_total";
        public const string VectorStoreSize = "vector_store_size_total";
        
        public const string LlmTokenUsage = "llm_token_usage_total";
        public const string LlmRequestDuration = "llm_request_duration";
        public const string LlmRequestCount = "llm_request_total";
        public const string LlmErrorCount = "llm_error_total";
        
        public const string DocumentProcessingDuration = "document_processing_duration";
        public const string DocumentProcessingCount = "document_processing_total";
        public const string DocumentProcessingErrorCount = "document_processing_error_total";
        
        public const string MemoryOperationDuration = "memory_operation_duration";
        public const string MemoryOperationCount = "memory_operation_total";
        public const string MemorySize = "memory_size_bytes";
        
        public const string ContentModerationCount = "content_moderation_total";
        public const string ContentRedactionCount = "content_redaction_total";
    }

    public static class MetricTags
    {
        public const string Method = "method";
        public const string Status = "status";
        public const string Endpoint = "endpoint";
        public const string Agent = "agent";
        public const string VectorStore = "vector_store";
        public const string Model = "model";
        public const string Provider = "provider";
        public const string DocumentType = "document_type";
        public const string MemoryType = "memory_type";
        public const string ErrorType = "error_type";
        public const string Operation = "operation";
        public const string Category = "category";
    }
}
