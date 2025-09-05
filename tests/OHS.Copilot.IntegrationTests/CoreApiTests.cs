using System.Net;
using OHS.Copilot.Application.DTOs.Requests;
using OHS.Copilot.Application.DTOs.Responses;

namespace OHS.Copilot.IntegrationTests;

public class FixedCoreApiTests : TestBase
{
    public FixedCoreApiTests(TestWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Health_ShouldReturnHealthy()
    {
        var response = await Client.GetAsync("/api/health");
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var healthResponse = await ParseResponseAsync<HealthResponse>(response);
        healthResponse.Status.Should().Be("Healthy");
        healthResponse.Version.Should().NotBeNullOrEmpty();
        healthResponse.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Metrics_ShouldReturnMetrics()
    {
        var response = await Client.GetAsync("/api/metrics");
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var metricsResponse = await ParseResponseAsync<MetricsResponse>(response);
        metricsResponse.Should().NotBeNull();
        metricsResponse.TotalRequests.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Ask_ShouldReturnValidResponse()
    {
        var request = new AskRequest
        {
            Question = "What are safety procedures for emergency evacuation?",
            MaxTokens = 2000
        };

        var response = await Client.PostAsync("/api/ask", CreateJsonContent(request));
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var askResponse = await ParseResponseAsync<AskResponse>(response);
        askResponse.Answer.Should().NotBeNullOrEmpty();
        askResponse.Citations.Should().NotBeEmpty();
        askResponse.Metadata.Should().NotBeNull();
        askResponse.Metadata.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task DraftLetter_ShouldReturnValidResponse()
    {
        var request = new DraftLetterRequest
        {
            Purpose = "incident notification",
            Points = new List<string> { "Investigation scheduled", "Documentation required" }
        };

        var response = await Client.PostAsync("/api/draft-letter", CreateJsonContent(request));
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var letterResponse = await ParseResponseAsync<DraftLetterResponse>(response);
        letterResponse.Subject.Should().NotBeNullOrEmpty();
        letterResponse.Body.Should().NotBeNullOrEmpty();
        letterResponse.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task Ingest_ShouldReturnValidResponse()
    {
        var request = new IngestRequest
        {
            DirectoryOrZipPath = "/home/mohammad/ohs/data/seed"
        };

        var response = await Client.PostAsync("/api/ingest", CreateJsonContent(request));
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ingestResponse = await ParseResponseAsync<IngestResponse>(response);
        ingestResponse.GeneratedChunks.Should().BeGreaterThan(0);
        ingestResponse.ProcessedFiles.Should().BeGreaterThan(0);
        ingestResponse.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task TestVectorStore_ShouldReturnValidResponse()
    {
        var response = await Client.PostAsync("/api/test-vector-store", new StringContent(""));
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Vector store test completed successfully");
    }
}
