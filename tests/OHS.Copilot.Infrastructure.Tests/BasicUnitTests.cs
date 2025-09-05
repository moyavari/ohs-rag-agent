using Microsoft.Extensions.Logging;
using OHS.Copilot.Domain.Entities;
using OHS.Copilot.Domain.ValueObjects;
using OHS.Copilot.Infrastructure.Configuration;
using OHS.Copilot.Infrastructure.Services;
using OHS.Copilot.Infrastructure.VectorStores;

namespace OHS.Copilot.Infrastructure.Tests;

public class BasicUnitTests
{
    [Fact]
    public void Chunk_Create_ShouldGenerateValidChunk()
    {
        // Act
        var chunk = Chunk.Create("Test content", "Test Title", "Test Section", "test://source");
        
        // Assert
        chunk.Should().NotBeNull();
        chunk.Id.Should().NotBeNullOrEmpty();
        chunk.Text.Should().Be("Test content");
        chunk.Title.Should().Be("Test Title");
        chunk.Section.Should().Be("Test Section");
        chunk.SourcePath.Should().Be("test://source");
    }
    
    [Fact]
    public void Answer_Create_ShouldGenerateValidAnswer()
    {
        // Arrange
        var citations = new List<Citation>
        {
            Citation.Create("test-1", 0.9, "Test Title", "Test content", "test://source")
        };
        
        // Act
        var answer = Answer.Create("This is a test answer", citations);
        
        // Assert
        answer.Should().NotBeNull();
        answer.Content.Should().Be("This is a test answer");
        answer.Citations.Should().HaveCount(1);
        answer.Citations.First().Id.Should().Be("test-1");
    }
    
    [Fact]
    public void DemoModeService_WithDemoModeEnabled_ShouldReturnTrue()
    {
        // Arrange
        var settings = new AppSettings { DemoMode = true };
        var logger = Mock.Of<ILogger<DemoModeService>>();
        var service = new DemoModeService(settings, logger);
        
        // Act
        var result = service.IsDemoModeEnabled();
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void DemoModeService_WithDemoModeDisabled_ShouldReturnFalse()
    {
        // Arrange
        var settings = new AppSettings { DemoMode = false };
        var logger = Mock.Of<ILogger<DemoModeService>>();
        var service = new DemoModeService(settings, logger);
        
        // Act
        var result = service.IsDemoModeEnabled();
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void AppSettings_Validate_ShouldNotThrowWithValidSettings()
    {
        // Arrange
        var settings = new AppSettings
        {
            DemoMode = true,
            AzureOpenAI = new AzureOpenAISettings 
            { 
                Endpoint = "https://test.openai.azure.com/", 
                ApiKey = "test-key",
                ChatDeployment = "gpt-4",
                EmbeddingDeployment = "text-embedding-ada-002"
            },
            VectorStore = new VectorStoreSettings { Type = "json" },
            Memory = new MemorySettings { Backend = "cosmos" }
        };
        
        // Act & Assert - should not throw
        var exception = Record.Exception(() => settings.Validate());
        exception.Should().BeNull();
    }
}