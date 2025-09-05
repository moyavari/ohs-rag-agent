using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using OHS.Copilot.Application.Interfaces;

namespace OHS.Copilot.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("DEMO_MODE", "true");
        Environment.SetEnvironmentVariable("VECTOR_STORE", "json");
        Environment.SetEnvironmentVariable("AOAI_ENDPOINT", "https://demo.openai.azure.com/");
        Environment.SetEnvironmentVariable("AOAI_API_KEY", "demo-key");
        Environment.SetEnvironmentVariable("AOAI_CHAT_DEPLOYMENT", "gpt-4");
        Environment.SetEnvironmentVariable("AOAI_EMB_DEPLOYMENT", "text-embedding-ada-002");
        Environment.SetEnvironmentVariable("MEMORY_BACKEND", "cosmos");

        builder.UseEnvironment("Testing");
        
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Critical);
        });
    }
}

public abstract class TestBase : IClassFixture<TestWebApplicationFactory>
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    protected TestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected StringContent CreateJsonContent<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    protected async Task<T> ParseResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions) ?? throw new InvalidOperationException("Failed to deserialize response");
    }
}
