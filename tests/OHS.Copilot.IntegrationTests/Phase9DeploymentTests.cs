using System.Net;

namespace OHS.Copilot.IntegrationTests;

public class Phase9DeploymentTests : TestBase
{
    public Phase9DeploymentTests(TestWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Phase9_DockerComposeSetup_ConfigurationIsValid()
    {
        var dockerComposeExists = File.Exists("/home/mohammad/ohs/docker-compose.override.yml");
        dockerComposeExists.Should().BeTrue("Docker Compose configuration should exist");

        var dockerfileExists = File.Exists("/home/mohammad/ohs/Dockerfile");
        dockerfileExists.Should().BeTrue("Dockerfile should exist for containerization");
    }

    [Fact]
    public async Task Phase9_DatabaseInitScripts_ArePresent()
    {
        var initScriptExists = File.Exists("/home/mohammad/ohs/scripts/init-postgres.sql");
        initScriptExists.Should().BeTrue("PostgreSQL initialization script should exist");

        var seedScriptExists = File.Exists("/home/mohammad/ohs/scripts/seed-postgres.sql");
        seedScriptExists.Should().BeTrue("PostgreSQL seed script should exist");

        var initScript = await File.ReadAllTextAsync("/home/mohammad/ohs/scripts/init-postgres.sql");
        initScript.Should().Contain("CREATE EXTENSION IF NOT EXISTS vector", "Should enable pgvector extension");
        initScript.Should().Contain("CREATE TABLE IF NOT EXISTS chunks", "Should create chunks table");
        initScript.Should().Contain("CREATE TABLE IF NOT EXISTS embeddings", "Should create embeddings table");
    }

    [Fact]
    public async Task Phase9_AzureBicepTemplates_AreComplete()
    {
        var mainBicepExists = File.Exists("/home/mohammad/ohs/deployment/azure/main.bicep");
        mainBicepExists.Should().BeTrue("Main Bicep template should exist");

        var parametersExists = File.Exists("/home/mohammad/ohs/deployment/azure/parameters.json");
        parametersExists.Should().BeTrue("Bicep parameters file should exist");

        var deployScriptExists = File.Exists("/home/mohammad/ohs/deployment/azure/deploy.sh");
        deployScriptExists.Should().BeTrue("Azure deployment script should exist");

        var mainBicep = await File.ReadAllTextAsync("/home/mohammad/ohs/deployment/azure/main.bicep");
        mainBicep.Should().Contain("Microsoft.CognitiveServices/accounts", "Should deploy Azure OpenAI");
        mainBicep.Should().Contain("Microsoft.App/containerApps", "Should deploy Container App");
        mainBicep.Should().Contain("Microsoft.KeyVault/vaults", "Should deploy Key Vault");
        mainBicep.Should().Contain("Microsoft.DocumentDB/databaseAccounts", "Should deploy Cosmos DB");
    }

    [Fact]
    public async Task Phase9_MonitoringConfiguration_IsSetup()
    {
        var prometheusConfigExists = File.Exists("/home/mohammad/ohs/monitoring/prometheus.yml");
        prometheusConfigExists.Should().BeTrue("Prometheus configuration should exist");

        var grafanaConfigExists = File.Exists("/home/mohammad/ohs/monitoring/grafana/datasources/prometheus.yml");
        grafanaConfigExists.Should().BeTrue("Grafana datasource configuration should exist");

        var prometheusConfig = await File.ReadAllTextAsync("/home/mohammad/ohs/monitoring/prometheus.yml");
        prometheusConfig.Should().Contain("ohs-copilot-api", "Should scrape OHS Copilot API metrics");
        prometheusConfig.Should().Contain("scrape_interval: 5s", "Should have proper scrape interval");
    }

    [Fact]
    public async Task Phase9_DeveloperDocumentation_IsComplete()
    {
        var readmeExists = File.Exists("/home/mohammad/ohs/README.md");
        readmeExists.Should().BeTrue("README.md should exist");

        var devDocsExists = File.Exists("/home/mohammad/ohs/docs/DEVELOPMENT.md");
        devDocsExists.Should().BeTrue("Development documentation should exist");

        // Environment variables are now documented in README.md

        var readme = await File.ReadAllTextAsync("/home/mohammad/ohs/README.md");
        readme.Should().Contain("Quick Start", "Should have quick start guide");
        readme.Should().Contain("docker compose up", "Should document Docker usage");
        readme.Should().Contain("Demo Mode", "Should document demo mode");
    }

    [Fact]
    public async Task Phase9_CiCdPipeline_IsConfigured()
    {
        var workflowExists = File.Exists("/home/mohammad/ohs/.github/workflows/ci-cd.yml");
        workflowExists.Should().BeTrue("GitHub Actions workflow should exist");

        var workflow = await File.ReadAllTextAsync("/home/mohammad/ohs/.github/workflows/ci-cd.yml");
        workflow.Should().Contain("dotnet test", "Should run tests in CI");
        workflow.Should().Contain("docker/build-push-action", "Should build and push container");
        workflow.Should().Contain("azure/login", "Should deploy to Azure");
    }

    [Fact]
    public async Task Phase9_ApplicationCanStart_InDemoMode()
    {
        var healthResponse = await Client.GetAsync("/api/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await healthResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy", "Application should be healthy in demo mode");
    }

    [Fact]
    public async Task Phase9_AllInfrastructureEndpoints_AreAccessible()
    {
        var endpoints = new[]
        {
            "/api/health",
            "/api/metrics", 
            "/api/demo-fixtures",
            "/api/demo-traces",
            "/api/golden-dataset",
            "/api/audit-logs",
            "/api/prompt-versions"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await Client.GetAsync(endpoint);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"Endpoint {endpoint} should be accessible");
        }
    }

    [Fact]
    public async Task Phase9_ApplicationHasProperHealthCheck()
    {
        var healthResponse = await Client.GetAsync("/api/health");
        var content = await healthResponse.Content.ReadAsStringAsync();
        
        content.Should().NotBeNullOrEmpty("Health endpoint should return JSON");
        content.Should().Contain("version", "Health response should include version");
        content.Should().Contain("timestamp", "Health response should include timestamp");
        content.Should().Contain("Healthy", "Health response should indicate healthy status");
    }

    [Fact]
    public async Task Phase9_DeploymentArtifacts_AreComplete()
    {
        var deploymentFiles = new[]
        {
            "/home/mohammad/ohs/Dockerfile",
            "/home/mohammad/ohs/docker-compose.override.yml", 
            "/home/mohammad/ohs/deployment/azure/main.bicep",
            "/home/mohammad/ohs/deployment/azure/parameters.json",
            "/home/mohammad/ohs/deployment/azure/deploy.sh",
            "/home/mohammad/ohs/.github/workflows/ci-cd.yml",
            "/home/mohammad/ohs/scripts/init-postgres.sql",
            "/home/mohammad/ohs/monitoring/prometheus.yml"
        };

        foreach (var file in deploymentFiles)
        {
            File.Exists(file).Should().BeTrue($"Deployment file {Path.GetFileName(file)} should exist");
        }
    }
}
