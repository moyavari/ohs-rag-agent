using System.Net;

namespace OHS.Copilot.IntegrationTests;

public class FrontendTests : TestBase
{
    public FrontendTests(TestWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public void Frontend_HTMLFile_IsValid()
    {
        var frontendFile = "/home/mohammad/ohs/frontend/index.html";
        File.Exists(frontendFile).Should().BeTrue("Frontend HTML file should exist");

        var content = File.ReadAllText(frontendFile);
        
        // Basic HTML structure validation
        content.Should().Contain("<!DOCTYPE html>", "Should be valid HTML5");
        content.Should().Contain("<html", "Should have html tag");
        content.Should().Contain("<head>", "Should have head section");
        content.Should().Contain("<body>", "Should have body section");
        content.Should().Contain("</html>", "Should be properly closed");

        // Required meta tags
        content.Should().Contain("charset=\"UTF-8\"", "Should specify UTF-8 encoding");
        content.Should().Contain("viewport", "Should be mobile responsive");
        
        // Title and branding
        content.Should().Contain("OHS Copilot", "Should have proper title");
        content.Should().Contain("Safety AI Assistant", "Should have descriptive subtitle");
    }

    [Fact]
    public void Frontend_HasRequiredFeatures()
    {
        var frontendContent = File.ReadAllText("/home/mohammad/ohs/frontend/index.html");
        
        // Should have main functional areas
        var requiredFeatures = new[]
        {
            "Ask Questions",           // Q&A functionality
            "Draft Letters",           // Letter drafting
            "Metrics",                 // System monitoring
            "Settings",               // Configuration
            "askForm",                // Ask form
            "draftForm",              // Draft form
            "apiRequest"              // API integration function
        };

        foreach (var feature in requiredFeatures)
        {
            frontendContent.Should().Contain(feature, $"Frontend should include {feature} functionality");
        }

        // Should have proper API integration
        frontendContent.Should().Contain("/api/ask", "Should integrate with ask API");
        frontendContent.Should().Contain("/api/draft-letter", "Should integrate with draft letter API");
        frontendContent.Should().Contain("/api/health", "Should check API health");
        frontendContent.Should().Contain("/api/metrics", "Should display metrics");
    }

    [Fact]
    public void Frontend_HasModernUIFeatures()
    {
        var frontendContent = File.ReadAllText("/home/mohammad/ohs/frontend/index.html");
        
        // Should have modern CSS features
        var modernFeatures = new[]
        {
            "display: flex",          // Modern layout
            "display: grid",          // CSS Grid
            "border-radius",          // Rounded corners
            "box-shadow",             // Modern shadows
            "transition",             // Smooth animations
            "@media",                 // Responsive design
            "linear-gradient"         // Modern gradients
        };

        foreach (var feature in modernFeatures)
        {
            frontendContent.Should().Contain(feature, $"Frontend should use modern CSS feature: {feature}");
        }

        // Should have JavaScript functionality
        var jsFeatures = new[]
        {
            "addEventListener",       // Modern event handling
            "async",                  // Async/await
            "fetch",                  // Modern HTTP client
            "JSON.stringify",         // JSON handling
            "localStorage"            // Persistent settings
        };

        foreach (var feature in jsFeatures)
        {
            frontendContent.Should().Contain(feature, $"Frontend should use modern JS feature: {feature}");
        }
    }

    [Fact]
    public void Frontend_StartupScript_IsExecutable()
    {
        var startupScript = "/home/mohammad/ohs/start-frontend.sh";
        File.Exists(startupScript).Should().BeTrue("Frontend startup script should exist");

        var scriptContent = File.ReadAllText(startupScript);
        scriptContent.Should().StartWith("#!/bin/bash", "Should be a valid bash script");
        
        // Should handle API startup
        scriptContent.Should().Contain("curl -s http://localhost:5000/api/health", "Should check API status");
        scriptContent.Should().Contain("dotnet run", "Should start API if needed");
        
        // Should handle web server startup
        scriptContent.Should().Contain("python3 -m http.server", "Should start HTTP server");
        scriptContent.Should().Contain("8080", "Should use port 8080 for frontend");
        
        // Should provide user guidance
        scriptContent.Should().Contain("Usage Tips", "Should provide usage instructions");
    }

    [Fact]
    public void Frontend_IntegratesWithAPI()
    {
        var frontendContent = File.ReadAllText("/home/mohammad/ohs/frontend/index.html");
        
        // Should integrate with all major API endpoints
        var apiEndpoints = new[]
        {
            "/api/ask",
            "/api/draft-letter",
            "/api/health",
            "/api/metrics",
            "/api/demo-fixtures",
            "/api/golden-dataset",
            "/api/audit-logs",
            "/api/conversations"
        };

        foreach (var endpoint in apiEndpoints)
        {
            frontendContent.Should().Contain(endpoint, $"Frontend should integrate with {endpoint}");
        }

        // Should handle errors properly
        frontendContent.Should().Contain("catch (error)", "Should have error handling");
        frontendContent.Should().Contain("showError", "Should display errors to users");
        
        // Should provide loading states
        frontendContent.Should().Contain("setLoading", "Should show loading indicators");
    }

    [Fact]
    public async Task Frontend_WorksWithRunningAPI()
    {
        // Test that the frontend can successfully communicate with the API
        
        // The API should be running from the test setup
        var healthResponse = await Client.GetAsync("/api/health");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK, "API should be running for frontend integration");

        // Test ask endpoint that frontend uses (this is the main functionality)
        var askRequest = new OHS.Copilot.Application.DTOs.Requests.AskRequest
        {
            Question = "Frontend integration test question",
            MaxTokens = 1000
        };

        var askResponse = await Client.PostAsync("/api/ask", CreateJsonContent(askRequest));
        askResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Frontend should be able to call ask API");

        var askResult = await ParseResponseAsync<OHS.Copilot.Application.DTOs.Responses.AskResponse>(askResponse);
        askResult.Answer.Should().NotBeNullOrEmpty("Frontend should receive valid responses");
        askResult.Metadata.Should().NotBeNull("Frontend should receive metadata as requested");

        // Verify metrics endpoint is accessible (used by frontend monitoring)
        var metricsResponse = await Client.GetAsync("/api/metrics");
        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Frontend should be able to access metrics");
    }

    [Fact]
    public void Frontend_HasUserExperienceFeatures()
    {
        var frontendContent = File.ReadAllText("/home/mohammad/ohs/frontend/index.html");
        
        // Should have good UX features
        var uxFeatures = new[]
        {
            "placeholder",            // Input placeholders
            "required",               // Form validation
            "focus",                  // Focus management
            "disabled",               // Loading states
            "classList",              // Dynamic styling
            "setTimeout",             // Timed actions
            "addEventListener"        // Event handling
        };

        foreach (var feature in uxFeatures)
        {
            frontendContent.Should().Contain(feature, $"Frontend should have UX feature: {feature}");
        }

        // Should have proper feedback mechanisms
        frontendContent.Should().Contain("success", "Should show success messages");
        frontendContent.Should().Contain("error", "Should show error messages");
        frontendContent.Should().Contain("loading", "Should show loading states");
    }

    [Fact]
    public void Frontend_HasResponsiveDesign()
    {
        var frontendContent = File.ReadAllText("/home/mohammad/ohs/frontend/index.html");
        
        // Should be mobile-friendly
        frontendContent.Should().Contain("@media", "Should have responsive CSS");
        frontendContent.Should().Contain("max-width", "Should handle small screens");
        frontendContent.Should().Contain("flex-wrap", "Should adapt layout");
        
        // Should use modern CSS Grid and Flexbox
        frontendContent.Should().Contain("display: grid", "Should use CSS Grid for layout");
        frontendContent.Should().Contain("display: flex", "Should use Flexbox for components");
        frontendContent.Should().Contain("grid-template-columns", "Should have responsive grid");
    }

    [Fact]
    public void Frontend_SupportsAllDocumentedFeatures()
    {
        var frontendContent = File.ReadAllText("/home/mohammad/ohs/frontend/index.html");
        
        // Should support all major user workflows from documentation
        var workflows = new[]
        {
            "conversationId",         // Multi-turn conversations
            "maxTokens",              // Token configuration
            "citations",              // Source references
            "metadata",               // Processing details
            "correlation",            // Request tracking
            "points",                 // Letter points
            "tone"                    // Letter tone
        };

        foreach (var workflow in workflows)
        {
            frontendContent.Should().Contain(workflow, $"Frontend should support {workflow} workflow");
        }
    }

    [Fact]
    public void Frontend_HasCompleteDocumentation()
    {
        // Verify README documents the frontend
        var readmeContent = File.ReadAllText("/home/mohammad/ohs/README.md");
        readmeContent.Should().Contain("Web Interface", "README should document the web interface");
        readmeContent.Should().Contain("start-frontend.sh", "README should reference the startup script");
        readmeContent.Should().Contain("localhost:8080", "README should specify the frontend port");
        
        // Should explain frontend features
        var frontendFeatures = new[]
        {
            "Intuitive Q&A Interface",
            "Letter Drafting Tool", 
            "System Monitoring",
            "Conversation History"
        };

        foreach (var feature in frontendFeatures)
        {
            readmeContent.Should().Contain(feature, $"README should document frontend feature: {feature}");
        }
    }
}
