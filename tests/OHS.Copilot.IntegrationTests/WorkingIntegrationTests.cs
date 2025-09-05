using System.Net;
using OHS.Copilot.Application.DTOs.Requests;
using Microsoft.Extensions.DependencyInjection;
using OHS.Copilot.Application.Interfaces;

namespace OHS.Copilot.IntegrationTests;

public class WorkingIntegrationTests : TestBase
{
    public WorkingIntegrationTests(TestWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Phase1_CoreDomainModels_AreAccessible()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        
        services.GetService<IVectorStore>().Should().NotBeNull();
        services.GetService<IEmbeddingService>().Should().NotBeNull();
        services.GetService<IAuditService>().Should().NotBeNull();
        services.GetService<IMemoryService>().Should().NotBeNull();
    }

    [Fact]
    public async Task Phase2_VectorStoreAbstractions_AreWorking()
    {
        using var scope = Factory.Services.CreateScope();
        var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();

        var healthCheck = await vectorStore.HealthCheckAsync(CancellationToken.None);
        healthCheck.Should().BeTrue();

        var count = await vectorStore.GetCountAsync(CancellationToken.None);
        count.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Phase3_MinimalApiEndpoints_AreResponding()
    {
        var healthResponse = await Client.GetAsync("/api/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var metricsResponse = await Client.GetAsync("/api/metrics");
        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var testVectorResponse = await Client.PostAsync("/api/test-vector-store", new StringContent(""));
        testVectorResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Phase4_AgentPipeline_IsRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var agents = scope.ServiceProvider.GetServices<IAgent>();
        
        agents.Should().NotBeEmpty();
        agents.Should().Contain(a => a.Name == "Router");
        agents.Should().Contain(a => a.Name == "Retriever");
        agents.Should().Contain(a => a.Name == "Drafter");
        agents.Should().Contain(a => a.Name == "CiteChecker");
    }

    [Fact]
    public async Task Phase5_DocumentProcessing_ServicesRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var parsers = scope.ServiceProvider.GetServices<IDocumentParser>();
        var chunkingService = scope.ServiceProvider.GetService<ITextChunkingService>();
        
        parsers.Should().NotBeEmpty();
        chunkingService.Should().NotBeNull();
    }

    [Fact]
    public async Task Phase6_GovernanceFeatures_AreWorking()
    {
        var auditResponse = await Client.GetAsync("/api/audit-logs");
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var promptVersionsResponse = await Client.GetAsync("/api/prompt-versions");
        promptVersionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var moderationService = scope.ServiceProvider.GetService<IContentModerationService>();
        var redactionService = scope.ServiceProvider.GetService<IContentRedactionService>();
        
        moderationService.Should().NotBeNull();
        redactionService.Should().NotBeNull();
    }

    [Fact]
    public async Task Phase7_DemoModeAndEvaluation_AreWorking()
    {
        var demoFixturesResponse = await Client.GetAsync("/api/demo-fixtures");
        demoFixturesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var demoTracesResponse = await Client.GetAsync("/api/demo-traces");
        demoTracesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var goldenDatasetResponse = await Client.GetAsync("/api/golden-dataset");
        goldenDatasetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var demoModeService = scope.ServiceProvider.GetService<IDemoModeService>();
        var evaluationService = scope.ServiceProvider.GetService<IEvaluationService>();
        
        demoModeService.Should().NotBeNull();
        evaluationService.Should().NotBeNull();
    }

    [Fact]
    public async Task Phase8_TelemetryAndObservability_AreWorking()
    {
        using var scope = Factory.Services.CreateScope();
        var telemetryService = scope.ServiceProvider.GetService<ITelemetryService>();
        
        telemetryService.Should().NotBeNull();

        using var activity = telemetryService.StartActivity("TestOperation");
        activity.Should().NotBeNull();
        
        telemetryService.RecordRequestMetrics("POST", "/api/test", 200, TimeSpan.FromMilliseconds(100), 1024, 2048);
        telemetryService.RecordAgentMetrics("TestAgent", TimeSpan.FromMilliseconds(50), true);
    }

    [Fact]
    public async Task AskEndpoint_ReturnsResponse_InDemoMode()
    {
        var request = new AskRequest
        {
            Question = "What are safety procedures?",
            MaxTokens = 2000
        };

        var response = await Client.PostAsync("/api/ask", CreateJsonContent(request));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AllPhases_TelemetryIsBeingCaptured()
    {
        var request = new AskRequest
        {
            Question = "Integration test telemetry check",
            MaxTokens = 2000
        };

        var response = await Client.PostAsync("/api/ask", CreateJsonContent(request));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var metricsResponse = await Client.GetAsync("/api/metrics");
        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await metricsResponse.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

}
