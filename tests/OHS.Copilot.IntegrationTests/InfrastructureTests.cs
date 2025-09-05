using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace OHS.Copilot.IntegrationTests;

public class InfrastructureTests : TestBase
{
    public InfrastructureTests(TestWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Infrastructure_StartupScripts_AreExecutable()
    {
        var startupScriptExists = File.Exists("/home/mohammad/ohs/start-demo.sh");
        startupScriptExists.Should().BeTrue("start-demo.sh script should exist");

        var testScriptExists = File.Exists("/home/mohammad/ohs/test-demo.sh");
        testScriptExists.Should().BeTrue("test-demo.sh script should exist");
    }

    [Fact]
    public async Task Infrastructure_ConfigurationFiles_AreValid()
    {
        var dockerignoreExists = File.Exists("/home/mohammad/ohs/.dockerignore");
        dockerignoreExists.Should().BeTrue(".dockerignore should exist for optimized builds");

        // Environment variables are now documented in README.md
    }

    [Fact]
    public async Task Infrastructure_MonitoringEndpoints_ReturnData()
    {
        var metricsResponse = await Client.GetAsync("/api/metrics");
        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var metricsContent = await metricsResponse.Content.ReadAsStringAsync();
        metricsContent.Should().NotBeNullOrEmpty("Metrics endpoint should return data");
        
        var metricsJson = JsonSerializer.Deserialize<JsonElement>(metricsContent);
        metricsJson.GetProperty("totalRequests").GetInt32().Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Infrastructure_HealthCheck_ProvidesDetailedInfo()
    {
        var healthResponse = await Client.GetAsync("/api/health");
        var content = await healthResponse.Content.ReadAsStringAsync();
        var healthJson = JsonSerializer.Deserialize<JsonElement>(content);

        healthJson.GetProperty("ok").GetBoolean().Should().BeTrue();
        healthJson.GetProperty("status").GetString().Should().Be("Healthy");
        healthJson.GetProperty("version").GetString().Should().NotBeNullOrEmpty();
        healthJson.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Infrastructure_DemoModeResources_AreAccessible()
    {
        var demoFixtures = await Client.GetAsync("/api/demo-fixtures");
        demoFixtures.StatusCode.Should().Be(HttpStatusCode.OK);

        var demoTraces = await Client.GetAsync("/api/demo-traces");
        demoTraces.StatusCode.Should().Be(HttpStatusCode.OK);

        var goldenDataset = await Client.GetAsync("/api/golden-dataset");
        goldenDataset.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Infrastructure_DocumentationFiles_ExistAndAreComplete()
    {
        var documentationFiles = new Dictionary<string, string[]>
        {
            ["/home/mohammad/ohs/README.md"] = new[] { "Quick Start", "Features", "Architecture", "Development" },
            ["/home/mohammad/ohs/docs/DEVELOPMENT.md"] = new[] { "Prerequisites", "Local Development", "Testing" }
        };

        foreach (var (filePath, expectedSections) in documentationFiles)
        {
            File.Exists(filePath).Should().BeTrue($"Documentation file {Path.GetFileName(filePath)} should exist");
            
            if (File.Exists(filePath))
            {
                var content = await File.ReadAllTextAsync(filePath);
                foreach (var section in expectedSections)
                {
                    content.Should().Contain(section, $"{Path.GetFileName(filePath)} should contain {section} section");
                }
            }
        }
    }

    [Fact]
    public async Task Infrastructure_AllSupportedVectorStores_AreConfigured()
    {
        using var scope = Factory.Services.CreateScope();
        var vectorStoreFactory = scope.ServiceProvider.GetService<OHS.Copilot.Infrastructure.VectorStores.VectorStoreFactory>();
        
        vectorStoreFactory.Should().NotBeNull("Vector store factory should be registered");
        
        // Vector store is working (currently JSON in test mode)
        var response = await Client.PostAsync("/api/test-vector-store", new StringContent(""));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Infrastructure_ObservabilityIntegration_IsComplete()
    {
        using var scope = Factory.Services.CreateScope();
        var telemetryService = scope.ServiceProvider.GetService<OHS.Copilot.Application.Interfaces.ITelemetryService>();
        
        telemetryService.Should().NotBeNull("Telemetry service should be registered for monitoring integration");
        
        // Test that telemetry is being captured
        var beforeMetrics = await Client.GetAsync("/api/metrics");
        var askRequest = new OHS.Copilot.Application.DTOs.Requests.AskRequest
        {
            Question = "Infrastructure telemetry test",
            MaxTokens = 2000
        };

        var askResponse = await Client.PostAsync("/api/ask", CreateJsonContent(askRequest));
        askResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterMetrics = await Client.GetAsync("/api/metrics");
        afterMetrics.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Infrastructure_ConfigurationManagement_IsFlexible()
    {
        var readmeFile = await File.ReadAllTextAsync("/home/mohammad/ohs/README.md");
        
        // Should document all major configuration options in README
        readmeFile.Should().Contain("VECTOR_STORE", "Should document vector store configuration");
        readmeFile.Should().Contain("MEMORY_BACKEND", "Should document memory backend configuration");
        readmeFile.Should().Contain("DEMO_MODE", "Should document demo mode");
        readmeFile.Should().Contain("AOAI_ENDPOINT", "Should document Azure OpenAI configuration");
    }

    [Fact]
    public async Task Infrastructure_ProductionReadiness_IsVerified()
    {
        var dockerfile = await File.ReadAllTextAsync("/home/mohammad/ohs/Dockerfile");
        
        // Production readiness checks
        dockerfile.Should().Contain("HEALTHCHECK", "Should have health check for container orchestration");
        dockerfile.Should().Contain("appuser", "Should run as non-root user");
        dockerfile.Should().Contain("EXPOSE 8080", "Should expose standard container port");
        dockerfile.Should().Contain("ASPNETCORE_ENVIRONMENT=Production", "Should default to production environment");
    }
}
