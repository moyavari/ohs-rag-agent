# Development Guide - OHS Copilot

## 🏁 Quick Start for Developers

### 1. Prerequisites
- **.NET 9 SDK**: Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **Docker Desktop**: Required for dependencies ([docker.com](https://docker.com))
- **Git**: For version control
- **IDE**: Visual Studio, VS Code, or Rider

### 2. Clone and Setup
```bash
git clone <repository-url>
cd ohs
dotnet restore
```

### 3. Local Development Options

#### Option A: Demo Mode (No Dependencies)
```bash
# Start in demo mode (no Azure OpenAI required)
./start-demo.sh

# Test the endpoints
./test-demo.sh
```

#### Option B: Full Local Stack
```bash
# Start infrastructure
docker compose up -d postgres qdrant jaeger prometheus grafana

# Set environment variables
export AOAI_ENDPOINT="your-azure-openai-endpoint"
export AOAI_API_KEY="your-api-key"
export VECTOR_STORE="qdrant"
export PG_CONN_STR="Host=localhost;Port=5432;Database=ohscopilot;Username=ohsuser;Password=ohspass123"

# Run the application
dotnet run --project src/OHS.Copilot.API --urls "http://localhost:5000"
```

#### Option C: Containerized Development
```bash
# Build and run everything in Docker
docker compose up --build

# Application available at http://localhost:5000
```

## 🏗️ Architecture Overview

### Clean Architecture Layers
```
├── Domain/          # Core entities and value objects
├── Application/     # Use cases, DTOs, and interfaces  
├── Infrastructure/ # External integrations and data access
└── API/            # HTTP endpoints and configuration
```

### Key Design Patterns
- **Multi-Agent Pipeline**: Router → Retriever → Drafter → CiteChecker
- **Pluggable Components**: Vector stores, embedding services, memory backends
- **Clean Architecture**: Dependency inversion throughout
- **Command/Query Separation**: Clear separation of read/write operations

## 🔧 Development Workflow

### Making Changes
1. **Domain Changes**: Start in `OHS.Copilot.Domain/`
2. **New Features**: Add interfaces in `Application/`, implement in `Infrastructure/`
3. **API Changes**: Update endpoints in `API/Program.cs`
4. **Tests**: Add integration tests in `tests/OHS.Copilot.IntegrationTests/`

### Code Style
- **Functions**: Maximum 10 lines
- **Classes**: Use for mutable entities
- **Records**: Use for immutable value objects  
- **No Comments**: Self-documenting code preferred
- **SOLID Principles**: Follow dependency inversion

### Running Tests
```bash
# Integration tests
dotnet test tests/OHS.Copilot.IntegrationTests/

# All tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 🧩 Component Configuration

### Vector Stores
```bash
# In-memory (demo)
VECTOR_STORE=json

# Qdrant (recommended for development)  
VECTOR_STORE=qdrant
QDRANT_ENDPOINT=http://localhost:6333

# PostgreSQL with pgvector
VECTOR_STORE=postgres
PG_CONN_STR="Host=localhost;Port=5432;Database=ohscopilot;Username=ohsuser;Password=ohspass123"

# Azure Cosmos DB
VECTOR_STORE=cosmos
COSMOS_CONN_STR="AccountEndpoint=https://localhost:8081/;AccountKey=..."
```

### Memory Backends
```bash
# In-memory (demo/testing)
MEMORY_BACKEND=memory

# PostgreSQL (recommended)
MEMORY_BACKEND=postgres

# Azure Cosmos DB (production)
MEMORY_BACKEND=cosmos
```

### Observability
```bash
# Enable telemetry
TELEMETRY_ENABLED=true

# Jaeger tracing
JAEGER_ENDPOINT=http://localhost:14268

# Prometheus metrics
PROMETHEUS_ENABLED=true

# Azure Application Insights
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=..."
```

## 🐛 Debugging

### Common Issues

**"Azure OpenAI Endpoint is required"**
```bash
# Solution: Enable demo mode or set OpenAI credentials
export DEMO_MODE=true
```

**"Cannot consume 500 tokens. Only 400 tokens remaining."**
```bash
# Solution: Increase max tokens or use demo mode
export MAX_TOKENS_PER_REQUEST=2000
export DEMO_MODE=true
```

**Vector store initialization fails**
```bash
# Solution: Clean vector store data
rm -f fixtures/vectors.json
docker compose restart qdrant
```

### Debugging Tools

**Application Logs**
```bash
# View real-time logs
docker compose logs -f ohs-api

# View with correlation IDs
curl -H "X-Correlation-ID: debug-123" http://localhost:5000/api/health
```

**Database Inspection**
```bash
# PostgreSQL
docker compose exec postgres psql -U ohsuser -d ohscopilot -c "SELECT * FROM ohs_copilot.chunks LIMIT 5;"

# Qdrant
curl http://localhost:6333/collections
```

**Telemetry**
- Jaeger UI: http://localhost:16686
- Prometheus: http://localhost:9090  
- Grafana: http://localhost:3000 (admin/admin123)

## 📚 API Development

### Adding New Endpoints
1. **Create DTOs** in `Application/DTOs/`
2. **Define interfaces** in `Application/Interfaces/`
3. **Implement services** in `Infrastructure/Services/`
4. **Add endpoints** in `API/Program.cs`
5. **Write tests** in `IntegrationTests/`

### Example: New Endpoint
```csharp
// 1. Request/Response DTOs
public class ExampleRequest
{
    public string Input { get; set; } = string.Empty;
}

public class ExampleResponse  
{
    public string Output { get; set; } = string.Empty;
}

// 2. Service Interface
public interface IExampleService
{
    Task<ExampleResponse> ProcessAsync(ExampleRequest request);
}

// 3. Implementation
public class ExampleService : IExampleService
{
    public async Task<ExampleResponse> ProcessAsync(ExampleRequest request)
    {
        return new ExampleResponse { Output = $"Processed: {request.Input}" };
    }
}

// 4. API Endpoint
app.MapPost("/api/example", async (ExampleRequest request, IExampleService service) =>
{
    var response = await service.ProcessAsync(request);
    return Results.Ok(response);
});

// 5. Integration Test
[Fact]
public async Task Example_ShouldReturnValidResponse()
{
    var request = new ExampleRequest { Input = "test" };
    var response = await Client.PostAsync("/api/example", CreateJsonContent(request));
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

## 🔄 Agent Development

### Adding New Agents
1. **Inherit from BaseAgent**
2. **Implement ExecuteInternalAsync**
3. **Register in DI container**
4. **Add to orchestration pipeline**

### Example Agent
```csharp
public class CustomAgent : BaseAgent
{
    public override string Name => "Custom";

    public CustomAgent(Kernel kernel, ILogger<CustomAgent> logger) 
        : base(kernel, logger) { }

    protected override async Task<AgentResult> ExecuteInternalAsync(
        AgentContext context, 
        CancellationToken cancellationToken)
    {
        // Agent logic here
        return AgentResult.Successful(new { result = "success" });
    }
}
```

## 📊 Performance Guidelines

### Optimization Targets
- **Response Time**: < 2 seconds for Q&A
- **Throughput**: > 100 requests/minute
- **Memory Usage**: < 2GB per instance
- **Token Efficiency**: < 2000 tokens per request

### Monitoring Metrics
- `http_request_duration`: API response times
- `agent_execution_duration`: Individual agent performance
- `vector_search_duration`: Search operation latency
- `llm_token_usage_total`: Token consumption tracking

## 🧪 Testing Strategy

### Test Types
- **Unit Tests**: Individual components (when needed)
- **Integration Tests**: Full API endpoints with real dependencies
- **End-to-End Tests**: Complete user workflows
- **Performance Tests**: Load and stress testing

### Test Data Management
```bash
# Reset test data
docker compose down -v
docker compose up -d postgres qdrant
./scripts/seed-test-data.sh
```

## 🚢 Deployment

### Local Testing
```bash
# Test the Docker build
docker build -t ohs-copilot .
docker run -p 5000:8080 -e DEMO_MODE=true ohs-copilot

# Test the full stack
docker compose up --build
```

### Azure Deployment
```bash
# Deploy to Azure Container Apps
cd deployment/azure
./deploy.sh
```

## 🤝 Contributing Guidelines

1. **Follow Clean Architecture** patterns
2. **Keep functions under 10 lines**
3. **Use descriptive names** instead of comments
4. **Add integration tests** for all new features
5. **Update documentation** as you go

### Code Review Checklist
- [ ] Follows SOLID principles
- [ ] Has integration tests
- [ ] Proper error handling
- [ ] Telemetry/logging added
- [ ] Documentation updated
- [ ] Demo mode compatible

---

**Happy coding! 🎉**
