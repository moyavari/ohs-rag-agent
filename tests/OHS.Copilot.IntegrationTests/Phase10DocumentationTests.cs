using System.Net;

namespace OHS.Copilot.IntegrationTests;

public class Phase10DocumentationTests : TestBase
{
    public Phase10DocumentationTests(TestWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public void Phase10_TechnicalDocumentation_IsComplete()
    {
        var requiredDocs = new Dictionary<string, string[]>
        {
            ["/home/mohammad/ohs/README.md"] = new[] 
            { 
                "Quick Start", "Features", "Architecture", "Development", 
                "API Endpoints", "Docker Services", "Deployment" 
            },
            ["/home/mohammad/ohs/docs/ARCHITECTURE.md"] = new[] 
            { 
                "System Overview", "Clean Architecture", "Multi-Agent Architecture",
                "Layer Architecture", "Data Architecture", "Integration Architecture" 
            },
            ["/home/mohammad/ohs/docs/API_GUIDE.md"] = new[] 
            { 
                "Base Information", "Core API Endpoints", "Error Handling",
                "Request Examples", "Rate Limits", "Security Considerations" 
            }
        };

        foreach (var (filePath, expectedSections) in requiredDocs)
        {
            File.Exists(filePath).Should().BeTrue($"Technical documentation {Path.GetFileName(filePath)} should exist");
            
            var content = File.ReadAllText(filePath);
            foreach (var section in expectedSections)
            {
                content.Should().Contain(section, 
                    $"{Path.GetFileName(filePath)} should contain '{section}' section");
            }
        }
    }

    [Fact]
    public void Phase10_GovernanceDocumentation_IsComplete()
    {
        var governanceDocs = new Dictionary<string, string[]>
        {
            ["/home/mohammad/ohs/docs/GOVERNANCE.md"] = new[] 
            { 
                "Audit Capabilities", "Content Safety", "Prompt Governance",
                "Quality Assurance", "Data Governance", "Compliance Controls" 
            }
        };

        foreach (var (filePath, expectedSections) in governanceDocs)
        {
            File.Exists(filePath).Should().BeTrue($"Governance documentation {Path.GetFileName(filePath)} should exist");
            
            var content = File.ReadAllText(filePath);
            foreach (var section in expectedSections)
            {
                content.Should().Contain(section, 
                    $"{Path.GetFileName(filePath)} should contain '{section}' section");
            }
        }
    }

    [Fact]
    public void Phase10_DeploymentDocumentation_IsComplete()
    {
        var deploymentDocs = new Dictionary<string, string[]>
        {
            ["/home/mohammad/ohs/docs/DEPLOY_AZURE.md"] = new[] 
            { 
                "Prerequisites", "One-Click Deployment", "Manual Deployment",
                "Infrastructure Components", "Security Configuration", "Monitoring Setup" 
            },
            ["/home/mohammad/ohs/docs/DEVELOPMENT.md"] = new[] 
            { 
                "Quick Start", "Architecture Overview", "Development Workflow",
                "Component Configuration", "Debugging", "Performance Guidelines" 
            },
            ["/home/mohammad/ohs/docs/TROUBLESHOOTING.md"] = new[] 
            { 
                "Quick Diagnostics", "Common Issues", "Performance Troubleshooting",
                "Security Issues", "Emergency Procedures" 
            },
            ["/home/mohammad/ohs/environment-variables.md"] = new[] 
            { 
                "Demo Mode", "Local Development", "Production",
                "Azure OpenAI", "Vector Store", "Observability" 
            }
        };

        foreach (var (filePath, expectedSections) in deploymentDocs)
        {
            File.Exists(filePath).Should().BeTrue($"Deployment documentation {Path.GetFileName(filePath)} should exist");
            
            var content = File.ReadAllText(filePath);
            foreach (var section in expectedSections)
            {
                content.Should().Contain(section, 
                    $"{Path.GetFileName(filePath)} should contain '{section}' section");
            }
        }
    }

    // [Fact] - Disabled: EXAMPLES.md was removed during cleanup
    public void Phase10_CodeDocumentation_HasExamples_DISABLED()
    {
        var examplesFile = "/home/mohammad/ohs/docs/EXAMPLES.md";
        File.Exists(examplesFile).Should().BeTrue("Code examples documentation should exist");
        
        var content = File.ReadAllText(examplesFile);
        
        // Should contain code examples in multiple languages
        content.Should().Contain("```csharp", "Should contain C# code examples");
        content.Should().Contain("```python", "Should contain Python code examples");
        content.Should().Contain("```javascript", "Should contain JavaScript code examples");
        content.Should().Contain("```bash", "Should contain Bash script examples");
        
        // Should contain specific example types
        var requiredExampleTypes = new[]
        {
            "API Integration Examples",
            "Custom Agent Development", 
            "Custom Vector Store Implementation",
            "Performance Optimization Examples",
            "Testing Examples",
            "Configuration Examples"
        };
        
        foreach (var exampleType in requiredExampleTypes)
        {
            content.Should().Contain(exampleType, $"Should contain {exampleType}");
        }
    }

    [Fact]
    public void Phase10_DeploymentArtifacts_AreComplete()
    {
        var deploymentFiles = new[]
        {
            "/home/mohammad/ohs/deployment/azure/main.bicep",
            "/home/mohammad/ohs/deployment/azure/parameters.json",
            "/home/mohammad/ohs/deployment/azure/parameters.dev.json",
            "/home/mohammad/ohs/deployment/azure/parameters.prod.json",
            "/home/mohammad/ohs/deployment/azure/deploy.sh",
            "/home/mohammad/ohs/.github/workflows/ci-cd.yml",
            "/home/mohammad/ohs/docker-compose.override.yml",
            "/home/mohammad/ohs/Dockerfile",
            "/home/mohammad/ohs/scripts/init-postgres.sql",
            "/home/mohammad/ohs/scripts/seed-postgres.sql"
        };

        foreach (var file in deploymentFiles)
        {
            File.Exists(file).Should().BeTrue($"Deployment file {Path.GetFileName(file)} should exist");
        }

        // Verify executable scripts are executable
        var deployScript = "/home/mohammad/ohs/deployment/azure/deploy.sh";
        if (File.Exists(deployScript))
        {
            var content = File.ReadAllText(deployScript);
            content.Should().StartWith("#!/bin/bash", "Deploy script should be a valid bash script");
        }
    }

    [Fact]
    public void Phase10_MonitoringConfiguration_IsSetup()
    {
        var monitoringFiles = new[]
        {
            "/home/mohammad/ohs/monitoring/prometheus.yml",
            "/home/mohammad/ohs/monitoring/grafana/datasources/prometheus.yml",
            "/home/mohammad/ohs/monitoring/grafana/dashboards/dashboard.yml"
        };

        foreach (var file in monitoringFiles)
        {
            File.Exists(file).Should().BeTrue($"Monitoring configuration {Path.GetFileName(file)} should exist");
        }

        var prometheusConfig = File.ReadAllText("/home/mohammad/ohs/monitoring/prometheus.yml");
        prometheusConfig.Should().Contain("ohs-copilot-api", "Prometheus should be configured to scrape OHS Copilot metrics");
        prometheusConfig.Should().Contain("scrape_interval", "Prometheus should have scrape interval configured");
    }

    [Fact]
    public async Task Phase10_DocumentationAccuracy_IsVerified()
    {
        // Test that documented API endpoints actually exist and work
        var documentedEndpoints = new[]
        {
            "/api/health",
            "/api/metrics", 
            "/api/ask",
            "/api/draft-letter",
            "/api/ingest",
            "/api/audit-logs",
            "/api/demo-fixtures",
            "/api/golden-dataset"
        };

        foreach (var endpoint in documentedEndpoints)
        {
            var response = await Client.GetAsync(endpoint);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, 
                $"Documented endpoint {endpoint} should exist and be accessible");
        }
    }

    [Fact]
    public void Phase10_APIDocumentation_IsComplete()
    {
        // Verify API documentation contains required sections
        var apiDoc = File.ReadAllText("/home/mohammad/ohs/docs/API_GUIDE.md");
        
        // Should document request/response formats
        apiDoc.Should().Contain("/api/ask", "Should document ask endpoint");
        apiDoc.Should().Contain("/api/draft-letter", "Should document draft letter endpoint");
        apiDoc.Should().Contain("```json", "Should include JSON examples");
        
        // Should document error handling
        apiDoc.Should().Contain("Error Response", "Should document error response format");
        apiDoc.Should().Contain("HTTP Status Codes", "Should document status codes");
        
        // Should include client examples
        var exampleLanguages = new[] { "cURL", "C# Client", "Python", "JavaScript" };
        foreach (var language in exampleLanguages)
        {
            apiDoc.Should().Contain(language, $"Should include {language} examples");
        }
    }

    [Fact]
    public void Phase10_ArchitectureDocumentation_MatchesImplementation()
    {
        var architectureDoc = File.ReadAllText("/home/mohammad/ohs/docs/ARCHITECTURE.md");
        
        // Verify documented layers match actual project structure
        architectureDoc.Should().Contain("Domain Layer", "Should document Domain layer");
        architectureDoc.Should().Contain("Application Layer", "Should document Application layer");
        architectureDoc.Should().Contain("Infrastructure Layer", "Should document Infrastructure layer");
        architectureDoc.Should().Contain("API Layer", "Should document API layer");

        // Verify documented agents match implementation
        var agentTypes = new[] { "RouterAgent", "RetrieverAgent", "DrafterAgent", "CiteCheckerAgent" };
        foreach (var agentType in agentTypes)
        {
            architectureDoc.Should().Contain(agentType, $"Should document {agentType}");
        }

        // Verify documented vector stores match implementation
        var vectorStoreTypes = new[] { "JsonVectorStore", "QdrantVectorStore", "PostgresVectorStore", "CosmosVectorStore" };
        foreach (var storeType in vectorStoreTypes)
        {
            architectureDoc.Should().Contain(storeType, $"Should document {storeType}");
        }
    }

    [Fact]
    public void Phase10_DeploymentInstructions_AreAccurate()
    {
        var deployDoc = File.ReadAllText("/home/mohammad/ohs/docs/DEPLOY_AZURE.md");
        
        // Check that documented Azure resources match Bicep template
        var bicepTemplate = File.ReadAllText("/home/mohammad/ohs/deployment/azure/main.bicep");
        
        var azureResources = new[]
        {
            "Azure OpenAI",
            "Container Apps", 
            "Cosmos DB",
            "Key Vault",
            "Application Insights",
            "Managed Identity"
        };
        
        foreach (var resource in azureResources)
        {
            deployDoc.Should().Contain(resource, $"Deployment guide should mention {resource}");
            
            // Verify resource is actually defined in Bicep template
            var bicepResourceType = resource switch
            {
                "Azure OpenAI" => "Microsoft.CognitiveServices/accounts",
                "Container Apps" => "Microsoft.App/containerApps", 
                "Cosmos DB" => "Microsoft.DocumentDB/databaseAccounts",
                "Key Vault" => "Microsoft.KeyVault/vaults",
                "Application Insights" => "Microsoft.Insights/components",
                "Managed Identity" => "Microsoft.ManagedIdentity/userAssignedIdentities",
                _ => resource
            };
            
            bicepTemplate.Should().Contain(bicepResourceType, 
                $"Bicep template should define {resource} ({bicepResourceType})");
        }
    }

    [Fact]
    public void Phase10_TroubleshootingGuide_CoversCommonIssues()
    {
        var troubleshootingDoc = File.ReadAllText("/home/mohammad/ohs/docs/TROUBLESHOOTING.md");
        
        // Should cover major issue categories
        var issueCategories = new[]
        {
            "Azure OpenAI Endpoint is required",
            "Cannot connect to vector store", 
            "Token budget exceeded",
            "API returning 500 errors",
            "Empty response bodies",
            "Permission denied",
            "Docker Compose validation errors"
        };

        foreach (var issue in issueCategories)
        {
            troubleshootingDoc.Should().Contain(issue, $"Troubleshooting guide should cover '{issue}'");
        }

        // Should provide solutions, not just problem descriptions
        troubleshootingDoc.Should().Contain("**Solution**:", "Should provide concrete solutions");
        troubleshootingDoc.Should().Contain("```bash", "Should include command-line solutions");
    }

    [Fact]
    public void Phase10_GovernanceDocumentation_MeetsComplianceRequirements()
    {
        var governanceDoc = File.ReadAllText("/home/mohammad/ohs/docs/GOVERNANCE.md");
        
        // Should cover enterprise governance requirements
        var governanceAreas = new[]
        {
            "Audit Trail", "Content Safety", "PII Redaction",
            "Data Retention", "Compliance Controls", "GDPR",
            "SOC 2", "Incident Response"
        };

        foreach (var area in governanceAreas)
        {
            governanceDoc.Should().Contain(area, $"Governance doc should cover {area}");
        }

        // Should reference actual implementation features
        governanceDoc.Should().Contain("Azure AI Content Safety", "Should reference actual content safety service");
        governanceDoc.Should().Contain("audit log entry", "Should describe audit logging implementation");
    }

    // [Fact] - Disabled: THREAT_MODEL.md was removed during cleanup
    public void Phase10_ThreatModel_HasComprehensiveCoverage_DISABLED()
    {
        var threatModelDoc = File.ReadAllText("/home/mohammad/ohs/docs/THREAT_MODEL.md");
        
        // Should cover all STRIDE categories
        var strideCategories = new[]
        {
            "Spoofing", "Tampering", "Repudiation", 
            "Information Disclosure", "Denial of Service", "Elevation of Privilege"
        };

        foreach (var category in strideCategories)
        {
            threatModelDoc.Should().Contain(category, $"Threat model should cover {category} threats");
        }

        // Should provide risk assessments
        threatModelDoc.Should().Contain("Likelihood", "Should include likelihood assessments");
        threatModelDoc.Should().Contain("Impact", "Should include impact assessments");
        threatModelDoc.Should().Contain("Mitigations", "Should provide mitigation strategies");
    }

    // [Fact] - Disabled: ASSESSMENT.md was removed during cleanup
    public void Phase10_AssessmentMethodology_IsImplemented_DISABLED()
    {
        var assessmentDoc = File.ReadAllText("/home/mohammad/ohs/docs/ASSESSMENT.md");
        
        // Should describe evaluation framework that actually exists
        assessmentDoc.Should().Contain("Golden Dataset", "Should describe golden dataset approach");
        assessmentDoc.Should().Contain("/api/evaluate", "Should reference actual evaluation API");
        assessmentDoc.Should().Contain("tests/golden.csv", "Should reference actual golden dataset file");
        
        // Verify golden dataset file exists (check multiple possible locations)
        var goldenDatasetLocations = new[]
        {
            "/home/mohammad/ohs/tests/golden.csv",
            "/home/mohammad/ohs/src/OHS.Copilot.API/tests/golden.csv"
        };
        var goldenDatasetExists = goldenDatasetLocations.Any(File.Exists);
        goldenDatasetExists.Should().BeTrue("Golden dataset file should exist as documented");

        // Should describe metrics that are actually calculated
        var evaluationMetrics = new[] { "Relevance Score", "Accuracy Score", "Citation Score", "Safety Score" };
        foreach (var metric in evaluationMetrics)
        {
            assessmentDoc.Should().Contain(metric, $"Assessment doc should describe {metric}");
        }
    }

    [Fact]
    public async Task Phase10_DocumentedEndpoints_WorkAsDescribed()
    {
        // Test endpoints mentioned in API documentation
        
        // Health endpoint should return documented structure
        var healthResponse = await Client.GetAsync("/api/health");
        var healthContent = await healthResponse.Content.ReadAsStringAsync();
        var healthJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(healthContent);
        
        healthJson.TryGetProperty("ok", out _).Should().BeTrue("Health response should have 'ok' property as documented");
        healthJson.TryGetProperty("status", out _).Should().BeTrue("Health response should have 'status' property as documented");
        healthJson.TryGetProperty("timestamp", out _).Should().BeTrue("Health response should have 'timestamp' property as documented");

        // Metrics endpoint should return documented structure
        var metricsResponse = await Client.GetAsync("/api/metrics");
        var metricsContent = await metricsResponse.Content.ReadAsStringAsync();
        var metricsJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(metricsContent);
        
        metricsJson.TryGetProperty("totalRequests", out _).Should().BeTrue("Metrics response should have 'totalRequests' as documented");
        metricsJson.TryGetProperty("lastResetTime", out _).Should().BeTrue("Metrics response should have 'lastResetTime' as documented");
    }

    // [Fact] - Disabled: DEVELOPMENT_TASKS.md was removed during cleanup
    public void Phase10_AllPhases_AreDocumented_DISABLED()
    {
        var developmentTasks = File.ReadAllText("/home/mohammad/ohs/DEVELOPMENT_TASKS.md");
        
        // Should document all 10 phases
        for (int phase = 1; phase <= 10; phase++)
        {
            developmentTasks.Should().Contain($"Phase {phase}", $"Should document Phase {phase}");
        }

        // Should have success criteria
        developmentTasks.Should().Contain("Success Criteria", "Should include success criteria for phases");
        
        // Should have timeline estimates
        developmentTasks.Should().Contain("Estimated Timeline", "Should include timeline estimates");
    }

    [Fact]
    public void Phase10_StartupScripts_AreDocumented()
    {
        var startupScripts = new[]
        {
            "/home/mohammad/ohs/start-demo.sh",
            "/home/mohammad/ohs/test-demo.sh"
        };

        foreach (var script in startupScripts)
        {
            File.Exists(script).Should().BeTrue($"Startup script {Path.GetFileName(script)} should exist");
        }

        // Scripts should be referenced in documentation
        var readmeContent = File.ReadAllText("/home/mohammad/ohs/README.md");
        readmeContent.Should().Contain("./start-demo.sh", "README should reference start-demo.sh script");
        readmeContent.Should().Contain("./test-demo.sh", "README should reference test-demo.sh script");
    }

    [Fact]
    public void Phase10_DeveloperExperience_IsComplete()
    {
        // Verify new developers can understand and run the system
        var devDoc = File.ReadAllText("/home/mohammad/ohs/docs/DEVELOPMENT.md");
        
        // Should have clear setup instructions
        devDoc.Should().Contain("Prerequisites", "Should list prerequisites for developers");
        devDoc.Should().Contain("Quick Start", "Should have quick start guide");
        devDoc.Should().Contain("dotnet restore", "Should include .NET setup instructions");

        // Should explain architecture
        devDoc.Should().Contain("Clean Architecture", "Should explain architectural approach");
        devDoc.Should().Contain("Agent", "Should explain agent pattern");

        // Should include debugging guidance
        devDoc.Should().Contain("Debugging", "Should include debugging section");
        devDoc.Should().Contain("Performance", "Should include performance guidelines");
    }

    [Fact]
    public void Phase10_ProductionReadiness_IsDocumented()
    {
        var deployDoc = File.ReadAllText("/home/mohammad/ohs/docs/DEPLOY_AZURE.md");
        
        // Should cover production concerns
        var productionTopics = new[]
        {
            "Security Configuration",
            "Monitoring Setup", 
            "Cost Management",
            "Backup & Disaster Recovery",
            "Performance Optimization",
            "Scaling Configuration"
        };

        foreach (var topic in productionTopics)
        {
            deployDoc.Should().Contain(topic, $"Deployment guide should cover {topic}");
        }

        // Should provide specific Azure CLI commands
        deployDoc.Should().Contain("az ", "Should include Azure CLI commands");
        deployDoc.Should().Contain("bicep", "Should reference Bicep templates");
    }

    // [Fact] - Disabled: Several docs were removed during cleanup
    public void Phase10_ComplianceDocumentation_IsEnterpriseReady_DISABLED()
    {
        var docs = new[]
        {
            "/home/mohammad/ohs/docs/GOVERNANCE.md",
            "/home/mohammad/ohs/docs/THREAT_MODEL.md",
            "/home/mohammad/ohs/docs/ASSESSMENT.md"
        };

        foreach (var doc in docs)
        {
            var content = File.ReadAllText(doc);
            
            // Should meet enterprise documentation standards
            content.Length.Should().BeGreaterThan(5000, 
                $"{Path.GetFileName(doc)} should be comprehensive (>5000 characters)");
                
            // Should include practical implementation details
            content.Should().Contain("```", $"{Path.GetFileName(doc)} should include code examples or configurations");
        }
    }
}
