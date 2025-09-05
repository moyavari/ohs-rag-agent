using OHS.Copilot.Infrastructure.Configuration;
using OHS.Copilot.Infrastructure.Extensions;
using OHS.Copilot.Infrastructure.Middleware;
using OHS.Copilot.Infrastructure.Observability;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Application.Services;
using OHS.Copilot.Application.DTOs.Requests;
using OHS.Copilot.Application.DTOs.Responses;
using OHS.Copilot.Domain.Entities;
using System.Reflection;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

try
{
    ConfigureServices(builder);
    var app = builder.Build();
    ConfigurePipeline(app);
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Application failed to start: {ex.Message}");
    Environment.Exit(1);
}

void ConfigureServices(WebApplicationBuilder builder)
{
    builder.Services.AddAppConfiguration(builder.Configuration);
    
    // Get the configured AppSettings from DI  
    var serviceProvider = builder.Services.BuildServiceProvider();
    var appSettings = serviceProvider.GetRequiredService<AppSettings>();

    // Add Vector Store and Embedding Services
    builder.Services.AddVectorStore(appSettings);
    builder.Services.AddEmbeddingService(appSettings);
    
    // Add Semantic Kernel and Agents
    builder.Services.AddSemanticKernel(appSettings);
    builder.Services.AddAgents();
    
    // Add Document Processing
    builder.Services.AddDocumentProcessing();
    
    // Add Governance Services
    builder.Services.AddGovernanceServices(appSettings);
    
    // Add Enhanced Observability
    builder.Services.AddObservability(appSettings);

    // Configure JSON serialization for minimal APIs
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.WriteIndented = true;
    });

    // Add CORS for frontend communication
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", builder =>
        {
            builder
                .WithOrigins("http://localhost:8080", "http://127.0.0.1:8080")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
}

void ConfigurePipeline(WebApplication app)
{
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<CorrelationMiddleware>();
    app.UseMiddleware<TelemetryMiddleware>();
    
    // Enable CORS
    app.UseCors("AllowFrontend");
    
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    MapApiEndpoints(app);
}

void MapApiEndpoints(WebApplication app)
{
    var version = GetApplicationVersion();
    
    app.MapGet("/api/health", (IServiceProvider services) =>
    {
        var appSettings = services.GetRequiredService<AppSettings>();
        var response = HealthResponse.Healthy(version);
        
        // Add vector store status
        response.Dependencies["vectorStore"] = new ComponentHealth
        {
            Healthy = true,
            Status = appSettings.VectorStore.Type.ToLower(),
            ResponseTime = TimeSpan.Zero
        };
        
        return Results.Ok(response);
    })
    .WithName("GetHealth")
    .WithTags("System");

    app.MapGet("/api/metrics", () =>
    {
        var metrics = new MetricsResponse
        {
            TotalRequests = 0,
            LastResetTime = DateTime.UtcNow,
            Format = "json"
        };
        return Results.Ok(metrics);
    })
    .WithName("GetMetrics")
    .WithTags("System");

    app.MapPost("/api/ask", async (AskRequest request, AgentOrchestrationService orchestration) =>
    {
        try
        {
            var response = await orchestration.ProcessAskRequestAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Ask request failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("Ask")
    .WithTags("AI");

    app.MapPost("/api/draft-letter", async (DraftLetterRequest request, AgentOrchestrationService orchestration) =>
    {
        try
        {
            var response = await orchestration.ProcessDraftLetterRequestAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Draft letter request failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("DraftLetter")
    .WithTags("AI");

    app.MapPost("/api/ingest", async (IngestRequest request, DocumentIngestService ingestService) =>
    {
        try
        {
            var response = await ingestService.IngestAsync(request);
            
            if (response.HasErrors())
            {
                return Results.Json(response, statusCode: 207); // Multi-status for partial success
            }
            
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Ingest request failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("Ingest")
    .WithTags("Data");

    // Test endpoint for Phase 2 verification
    app.MapPost("/api/test-vector-store", async (IVectorStore vectorStore, IEmbeddingService embeddingService) =>
    {
        try
        {
            // Pre-test: Check initial count
            var initialCount = await vectorStore.GetCountAsync();
            
            // Test data
            var testChunk = Chunk.Create(
                "Test safety protocol for equipment handling - DIRECT TEST", 
                "Safety Protocols", 
                "Equipment Handling", 
                "test-doc.pdf");

            // Generate embedding
            var embedding = await embeddingService.GenerateEmbeddingAsync(testChunk.Text);
            
            // Store the vector
            await vectorStore.UpsertAsync(testChunk, embedding);
            
            // Immediate count check
            var afterUpsertCount = await vectorStore.GetCountAsync();
            
            // Try to read back the document
            var readBack = await vectorStore.GetByIdAsync(testChunk.Id);
            
            // Search for similar vectors
            var searchResults = await vectorStore.SearchAsync(embedding, topK: 5, minScore: 0.0);
            
            // Final count
            var finalCount = await vectorStore.GetCountAsync();
            
            return Results.Ok(new
            {
                success = true,
                message = "Vector store test completed successfully",
                testChunkId = testChunk.Id,
                initialCount = initialCount,
                afterUpsertCount = afterUpsertCount,
                finalCount = finalCount,
                readBackSuccess = readBack != null,
                readBackText = readBack?.Text?.Substring(0, Math.Min(50, readBack?.Text?.Length ?? 0)) + "...",
                searchResultCount = searchResults.Count,
                vectorStoreType = vectorStore.GetType().Name,
                embeddingDimensions = embeddingService.GetEmbeddingDimensions(),
                embeddingModel = embeddingService.GetModelName()
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Vector store test failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("TestVectorStore")
    .WithTags("Testing");

    // Governance test endpoints
    app.MapPost("/api/test-redaction", async (IContentRedactionService redactionService) =>
    {
        try
        {
            var testData = "Contact John Doe at john.doe@example.com or call (555) 123-4567. SSN: 123-45-6789";
            var result = await redactionService.RedactContentAsync(testData);
            
            return Results.Ok(new
            {
                success = true,
                original = result.OriginalContent,
                redacted = result.RedactedContent,
                redactionCount = result.Redactions.Count,
                redactions = result.Redactions.Select(r => new { r.Type, r.OriginalValue, r.RedactedValue })
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Redaction test failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("TestRedaction")
    .WithTags("Testing", "Governance");

    app.MapPost("/api/test-moderation", async (IContentModerationService moderationService) =>
    {
        try
        {
            var testData = "This is a test message with potentially unsafe content for moderation testing.";
            var result = await moderationService.ModerateTextAsync(testData);
            
            return Results.Ok(new
            {
                success = true,
                content = testData,
                flagged = result.Flagged,
                action = result.Action.ToString(),
                severity = result.OverallSeverity,
                categories = result.Categories.Select(c => new { c.Name, c.Severity, c.Level })
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Moderation test failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("TestModeration")
    .WithTags("Testing", "Governance");

    app.MapGet("/api/audit-logs/{auditId?}", async (string? auditId, IAuditService auditService) =>
    {
        try
        {
            if (!string.IsNullOrEmpty(auditId))
            {
                var auditLog = await auditService.GetAuditLogAsync(auditId);
                return auditLog != null ? Results.Ok(auditLog) : Results.NotFound();
            }
            
            var count = await auditService.GetAuditLogCountAsync();
            return Results.Ok(new { totalAuditLogs = count, message = "Audit service is working" });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Audit log retrieval failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("GetAuditLogs")
    .WithTags("Governance");

    app.MapGet("/api/prompt-versions/{promptName?}", async (string? promptName, IPromptVersionService promptVersionService) =>
    {
        try
        {
            if (!string.IsNullOrEmpty(promptName))
            {
                var history = await promptVersionService.GetPromptHistoryAsync(promptName);
                return Results.Ok(new { promptName, versions = history });
            }
            
            var allHashes = await promptVersionService.GetAllPromptHashesAsync();
            return Results.Ok(new { totalPrompts = allHashes.Count, prompts = allHashes });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Prompt version retrieval failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("GetPromptVersions")
    .WithTags("Governance");

    // Memory management endpoints
    app.MapGet("/api/conversations/{conversationId}", async (string conversationId, IMemoryService memoryService) =>
    {
        try
        {
            var memory = await memoryService.GetConversationMemoryAsync(conversationId);
            return memory != null ? Results.Ok(memory) : Results.NotFound();
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to retrieve conversation: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("GetConversation")
    .WithTags("Memory");

    app.MapDelete("/api/conversations/{conversationId}", async (string conversationId, IMemoryService memoryService) =>
    {
        try
        {
            await memoryService.DeleteConversationMemoryAsync(conversationId);
            return Results.Ok(new { message = "Conversation deleted successfully" });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to delete conversation: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("DeleteConversation")
    .WithTags("Memory");

    app.MapPost("/api/personas/{userId}", async (string userId, PersonaRequest personaRequest, IMemoryService memoryService) =>
    {
        try
        {
            var persona = PersonaMemory.Create(userId, personaRequest.Type);
            
            if (personaRequest.CustomProfile.Count > 0)
            {
                foreach (var item in personaRequest.CustomProfile)
                {
                    persona.Profile[item.Key] = item.Value;
                }
            }

            await memoryService.SavePersonaMemoryAsync(userId, persona);
            
            return Results.Ok(new { message = "Persona created successfully", persona });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to create persona: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("CreatePersona")
    .WithTags("Memory");

    app.MapGet("/api/personas/{userId}", async (string userId, IMemoryService memoryService) =>
    {
        try
        {
            var persona = await memoryService.GetPersonaMemoryAsync(userId);
            return persona != null ? Results.Ok(persona) : Results.NotFound();
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to retrieve persona: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("GetPersona")
    .WithTags("Memory");

    app.MapGet("/api/policies", async (IMemoryService memoryService) =>
    {
        try
        {
            var policies = await memoryService.GetAllPolicyMemoryAsync();
            return Results.Ok(new { totalPolicies = policies.Count, policies });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to retrieve policies: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("GetPolicies")
    .WithTags("Memory");

    app.MapGet("/api/policies/search", async (string q, IMemoryService memoryService, int maxResults = 10) =>
    {
        try
        {
            var results = await memoryService.SearchPolicyMemoryAsync(q, maxResults);
            return Results.Ok(new { query = q, resultCount = results.Count, results });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Policy search failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("SearchPolicies")
    .WithTags("Memory");

    // Demo Mode and Evaluation endpoints
    app.MapPost("/api/evaluate", async (IEvaluationService evaluationService, EvaluationRequest? evalRequest = null) =>
    {
        try
        {
            var options = new EvaluationOptions();
            if (evalRequest != null)
            {
                options.RunInDemoMode = evalRequest.RunDemoMode;
                options.RunInLiveMode = evalRequest.RunLiveMode;
                options.MaxConcurrentRequests = evalRequest.MaxConcurrentRequests;
            }

            var report = await evaluationService.RunEvaluationAsync(options);
            
            return Results.Ok(new
            {
                reportId = report.ReportId,
                mode = report.Mode,
                demoMetrics = report.DemoModeMetrics,
                liveMetrics = report.LiveModeMetrics,
                summary = report.Summary,
                totalQuestions = report.Results.Count,
                successRate = report.DemoModeMetrics.OverallSuccessPercentage
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Evaluation failed: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("RunEvaluation")
    .WithTags("Evaluation");

    app.MapGet("/api/golden-dataset", async (IEvaluationService evaluationService) =>
    {
        try
        {
            var goldenData = await evaluationService.LoadGoldenDatasetAsync();
            return Results.Ok(new 
            { 
                totalQuestions = goldenData.Count,
                categories = goldenData.GroupBy(q => q.Category).ToDictionary(g => g.Key, g => g.Count()),
                questions = goldenData.Select(q => new { q.Id, q.Question, q.Category, q.MustContain })
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to load golden dataset: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("GetGoldenDataset")
    .WithTags("Evaluation");

    app.MapGet("/api/demo-fixtures", async (IDemoModeService demoModeService) =>
    {
        try
        {
            var fixtures = await demoModeService.LoadFixturesAsync();
            return Results.Ok(new
            {
                totalFixtures = fixtures.Count,
                askFixtures = fixtures.Count(f => f.Type == "ask"),
                letterFixtures = fixtures.Count(f => f.Type == "draft"),
                fixtures = fixtures.Select(f => new { f.Id, f.Type, f.Metadata })
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to load demo fixtures: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("GetDemoFixtures")
    .WithTags("Demo");

    app.MapGet("/api/demo-traces/{traceId?}", async (string? traceId, IDemoModeService demoModeService) =>
    {
        try
        {
            if (!string.IsNullOrEmpty(traceId))
            {
                var trace = await demoModeService.GetDemoTraceAsync(traceId);
                return trace != null ? Results.Ok(trace) : Results.NotFound();
            }

            return Results.Ok(new { message = "Provide traceId to retrieve specific demo trace" });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to retrieve demo trace: {ex.Message}", statusCode: 500);
        }
    })
    .WithName("GetDemoTrace")
    .WithTags("Demo");
}

string GetApplicationVersion()
{
    return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
}

public partial class Program { }
