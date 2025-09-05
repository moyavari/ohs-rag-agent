# Architecture Guide - OHS Copilot

## 🏗️ **System Overview**

OHS Copilot is an enterprise-grade RAG (Retrieval-Augmented Generation) system built using **Clean Architecture** principles with **Multi-Agent Orchestration**.

## 🎯 **Core Design Principles**

### **1. Clean Architecture**
```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│     Domain      │    │   Application    │    │ Infrastructure  │
│   (Entities &   │◄───│  (Use Cases &    │◄───│ (External Data  │
│ Value Objects)  │    │   Interfaces)    │    │ & Integrations) │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         ▲                        ▲                       ▲
         └────────────────────────┼───────────────────────┘
                                  │
                       ┌──────────────────┐
                       │       API        │
                       │ (Controllers &   │
                       │  Configuration)  │
                       └──────────────────┘
```

### **2. Dependency Inversion**
- All dependencies point inward toward the Domain
- Infrastructure depends on Application abstractions
- Business logic is isolated from external concerns

### **3. Pluggable Components**
- Vector stores: JSON, Qdrant, PostgreSQL, Cosmos DB
- Embedding services: Azure OpenAI, Demo mode
- Memory backends: In-memory, PostgreSQL, Cosmos DB

---

## 🤖 **Multi-Agent Architecture**

### **Agent Pipeline Flow**
```
HTTP Request
     │
     ▼
┌─────────────┐
│   Router    │ ──► Determines intent (ask, draft, ingest)
│   Agent     │     
└─────────────┘
     │
     ▼
┌─────────────┐
│ Retriever   │ ──► Searches vector store for relevant context
│   Agent     │     
└─────────────┘
     │
     ▼
┌─────────────┐
│   Drafter   │ ──► Generates response using LLM
│   Agent     │     
└─────────────┘
     │
     ▼
┌─────────────┐
│CiteChecker  │ ──► Validates citations and compliance
│   Agent     │     
└─────────────┘
     │
     ▼
HTTP Response
```

### **Agent Responsibilities**

#### **RouterAgent**
- **Purpose**: Intent classification and request routing
- **Input**: Raw user request
- **Output**: Classified intent and parameters
- **Technology**: Semantic Kernel with GPT-4

#### **RetrieverAgent** 
- **Purpose**: Context retrieval from knowledge base
- **Input**: Search query and parameters
- **Output**: Ranked document chunks with similarity scores
- **Technology**: Vector search with embeddings

#### **DrafterAgent**
- **Purpose**: Response generation using retrieved context
- **Input**: User question + relevant context chunks
- **Output**: Structured answer with citations
- **Technology**: Semantic Kernel with prompt engineering

#### **CiteCheckerAgent**
- **Purpose**: Citation validation and content safety
- **Input**: Generated response with citations
- **Output**: Validated response with compliance checks
- **Technology**: Azure AI Content Safety + PII redaction

---

## 🏛️ **Layer Architecture**

### **Domain Layer** (`src/OHS.Copilot.Domain/`)
**Pure business logic with no external dependencies**

```
Entities/                    # Mutable business objects
├── Chunk.cs                # Document segment
├── Embedding.cs            # Vector representation  
├── AuditLogEntry.cs        # Compliance tracking
├── ConversationMemory.cs   # Multi-turn dialogue
├── PersonaMemory.cs        # User profiles
└── PolicyMemory.cs         # Organizational policies

ValueObjects/               # Immutable value objects
├── Citation.cs             # Source reference
├── Answer.cs               # AI response
└── LetterDraft.cs          # Generated correspondence
```

### **Application Layer** (`src/OHS.Copilot.Application/`)
**Use cases and application services**

```
DTOs/                       # Data contracts
├── Requests/               # API input models
└── Responses/              # API output models

Interfaces/                 # Abstraction contracts
├── IVectorStore.cs         # Vector database operations
├── IEmbeddingService.cs    # Text embedding generation
├── IAgent.cs               # Agent execution contract
├── IAuditService.cs        # Compliance logging
├── IMemoryService.cs       # Conversation persistence
└── ITelemetryService.cs    # Observability

Services/                   # Orchestration logic
├── AgentOrchestrationService.cs  # Multi-agent pipeline
└── DocumentIngestService.cs      # Document processing
```

### **Infrastructure Layer** (`src/OHS.Copilot.Infrastructure/`)
**External integrations and data access**

```
VectorStores/               # Vector database implementations
├── JsonVectorStore.cs      # In-memory demo store
├── QdrantVectorStore.cs    # Qdrant integration
├── PostgresVectorStore.cs  # PostgreSQL + pgvector
└── CosmosVectorStore.cs    # Azure Cosmos DB

Agents/                     # Agent implementations
├── BaseAgent.cs            # Common agent functionality
├── RouterAgent.cs          # Intent classification
├── RetrieverAgent.cs       # Context retrieval
├── DrafterAgent.cs         # Response generation
└── CiteCheckerAgent.cs     # Citation validation

Services/                   # External service integrations
├── SemanticKernelService.cs      # Microsoft Semantic Kernel
├── AzureOpenAIEmbeddingService.cs # Azure OpenAI embeddings
├── AzureContentModerationService.cs # Content safety
├── DemoModeService.cs            # Fixture responses
└── EvaluationService.cs          # Quality assessment

Observability/              # Telemetry and monitoring
├── TelemetryService.cs     # Custom metrics and tracing
├── TelemetryMiddleware.cs  # HTTP request instrumentation
└── ObservabilityExtensions.cs # OpenTelemetry configuration

Middleware/                 # Cross-cutting concerns
├── CorrelationMiddleware.cs # Request correlation IDs
└── ErrorHandlingMiddleware.cs # Global error handling
```

### **API Layer** (`src/OHS.Copilot.API/`)
**HTTP interface and application configuration**

```
Program.cs                  # Application entry point and configuration
├── Service Registration    # Dependency injection setup
├── Middleware Pipeline     # Request processing pipeline  
├── API Endpoints          # RESTful HTTP endpoints
└── Observability Setup    # Telemetry and monitoring
```

---

## 📊 **Data Architecture**

### **Vector Storage Design**
```
Document Chunks ──► Text Embeddings ──► Vector Search
      │                    │                │
      ▼                    ▼                ▼
┌──────────┐     ┌─────────────┐    ┌─────────────┐
│  Chunk   │     │ Embedding   │    │   Search    │
│ Entity   │     │  (1536-d    │    │  Results    │
│          │     │  vectors)   │    │             │
└──────────┘     └─────────────┘    └─────────────┘
```

### **Memory Architecture**
```
User Session ──► Conversation ──► Multi-turn Context
     │             Memory              │
     ▼                                 ▼
┌──────────┐                    ┌─────────────┐
│ Persona  │                    │ Policy      │
│ Memory   │                    │ Memory      │
└──────────┘                    └─────────────┘
```

---

## 🔧 **Integration Architecture**

### **Vector Store Abstraction**
```
Application Layer
       │
       ▼ (IVectorStore)
┌──────────────────┐
│ VectorStore      │
│ Factory          │
└──────────────────┘
       │
       ├─► JsonVectorStore     (Demo/Testing)
       ├─► QdrantVectorStore   (Development)  
       ├─► PostgresVectorStore (Production)
       └─► CosmosVectorStore   (Azure Cloud)
```

### **Embedding Service Abstraction**
```
Application Layer
       │
       ▼ (IEmbeddingService)
┌──────────────────┐
│ Embedding        │
│ Factory          │
└──────────────────┘
       │
       ├─► AzureOpenAIEmbeddingService (Production)
       └─► DemoEmbeddingService        (Demo Mode)
```

---

## 🌊 **Request Flow Architecture**

### **1. HTTP Request Processing**
```
HTTP Request
     │
     ▼
┌─────────────────┐
│ ASP.NET Core    │
│ Minimal API     │
└─────────────────┘
     │
     ▼
┌─────────────────┐
│ Middleware      │
│ Pipeline        │
├─ Correlation    │ ──► Adds X-Correlation-ID
├─ Error Handling │ ──► Global exception handling  
└─ Telemetry      │ ──► OpenTelemetry instrumentation
└─────────────────┘
     │
     ▼
┌─────────────────┐
│ Endpoint        │
│ Handler         │
└─────────────────┘
```

### **2. Agent Orchestration Flow**
```
API Endpoint
     │
     ▼
┌─────────────────┐
│ Orchestration   │
│ Service         │
└─────────────────┘
     │
     ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Audit Service   │    │ Demo Mode       │    │ Memory Service  │
│ (Compliance)    │    │ (Fixtures)      │    │ (Context)       │
└─────────────────┘    └─────────────────┘    └─────────────────┘
     │
     ▼
┌─────────────────┐
│ Agent Pipeline  │
│ Execution       │
└─────────────────┘
```

### **3. Agent Execution Detail**
```
Agent Context
     │
     ▼
┌─────────────────┐
│ BaseAgent       │
├─ Telemetry      │ ──► Start activity span
├─ Error Handling │ ──► Catch and log exceptions
└─ Token Budgeting│ ──► Manage LLM token usage
└─────────────────┘
     │
     ▼
┌─────────────────┐
│ Specific Agent  │
│ Implementation  │
├─ RouterAgent    │
├─ RetrieverAgent │  
├─ DrafterAgent   │
└─ CiteChecker    │
└─────────────────┘
```

---

## 💾 **Data Storage Architecture**

### **Vector Storage Options**
| Store | Use Case | Performance | Scalability |
|-------|----------|-------------|-------------|
| **JSON** | Demo/Testing | Low | Single instance |
| **Qdrant** | Development | High | Horizontal scaling |
| **PostgreSQL** | Production | High | Vertical scaling |
| **Cosmos DB** | Azure Cloud | Very High | Global distribution |

### **Memory Storage Options**
| Backend | Use Case | Consistency | Features |
|---------|----------|-------------|----------|
| **In-Memory** | Testing | Session-only | Fast access |
| **PostgreSQL** | Production | ACID | Full SQL features |
| **Cosmos DB** | Azure | Eventually consistent | Global distribution |

---

## 🔐 **Security Architecture**

### **Defense in Depth**
```
Internet ──► WAF/CDN ──► API Gateway ──► Application ──► Database
   │            │           │             │             │
   ▼            ▼           ▼             ▼             ▼
TLS 1.3    Rate Limiting  Auth/AuthZ   Input Valid.  Encryption
           DDoS Protect   CORS         PII Redaction  at Rest
           GeoBlocking    API Keys     Content Safety
```

### **Content Safety Pipeline**
```
User Input
     │
     ▼
┌─────────────────┐
│ Content         │
│ Moderation      │ ──► Azure AI Content Safety
└─────────────────┘
     │
     ▼
┌─────────────────┐
│ PII Redaction   │ ──► Remove sensitive information
└─────────────────┘
     │
     ▼
┌─────────────────┐
│ Audit Logging   │ ──► Complete request/response audit
└─────────────────┘
```

---

## 📊 **Observability Architecture**

### **OpenTelemetry Integration**
```
Application Code
     │
     ▼
┌─────────────────┐
│ Instrumentation │
│ Libraries       │
├─ ASP.NET Core   │
├─ HTTP Client    │
├─ Entity Framework│
└─ Custom Agents  │
└─────────────────┘
     │
     ▼
┌─────────────────┐
│ OpenTelemetry   │
│ SDK             │
└─────────────────┘
     │
     ├─► Console Exporter     (Development)
     ├─► Jaeger Exporter      (Local tracing)
     ├─► Prometheus Exporter  (Metrics)
     └─► Application Insights (Azure)
```

### **Metrics Collection**
```
HTTP Requests ──► Request/Response metrics
Agent Execution ──► Individual agent performance  
Vector Operations ──► Search latency and accuracy
LLM Operations ──► Token usage and response time
Memory Operations ──► Cache hit/miss rates
Custom Business ──► Domain-specific metrics
```

---

## 🚀 **Deployment Architecture**

### **Local Development**
```
Developer Machine
     │
     ├─► Docker Compose Stack
     │   ├─ PostgreSQL + pgvector
     │   ├─ Qdrant Vector DB
     │   ├─ Jaeger Tracing
     │   ├─ Prometheus + Grafana
     │   └─ Cosmos DB Emulator
     │
     └─► .NET Application
         ├─ Demo Mode (No Azure)
         ├─ Local Vector Store
         └─ In-Memory Services
```

### **Azure Production**
```
Internet ──► Azure Front Door ──► Container Apps ──► Backend Services
                     │                  │                │
                     ▼                  ▼                ▼
              ┌─────────────┐    ┌─────────────┐  ┌─────────────┐
              │ Web App     │    │ API Pods    │  │ Azure       │
              │ Firewall    │    │ (Scaling)   │  │ Services    │
              │             │    │             │  │             │
              │ - DDoS      │    │ - Health    │  │ - OpenAI    │
              │ - SSL       │    │ - Metrics   │  │ - Cosmos DB │
              │ - Rate      │    │ - Logs      │  │ - Key Vault │
              │   Limiting  │    │ - Traces    │  │ - App       │
              └─────────────┘    └─────────────┘  │   Insights  │
                                                  └─────────────┘
```

---

## 🔄 **Configuration Architecture**

### **Environment-Based Configuration**
```
Configuration Sources (Hierarchy)
     │
     ├─► Environment Variables (Highest priority)
     ├─► appsettings.{Environment}.json
     ├─► appsettings.json  
     ├─► Azure Key Vault (Production)
     └─► Command Line Arguments
```

### **Configuration Categories**
- **Application**: Logging, environment, URLs
- **Azure OpenAI**: Endpoints, keys, deployment names
- **Vector Store**: Database connections and settings
- **Memory Backend**: Persistence layer configuration  
- **Observability**: Telemetry exporters and sampling
- **Security**: Content safety and PII redaction
- **Performance**: Token limits and concurrency

---

## 📈 **Scalability Architecture**

### **Horizontal Scaling Points**
```
Load Balancer
     │
     ├─► API Instance 1 ──┐
     ├─► API Instance 2 ──┤ ──► Shared Vector Store
     ├─► API Instance 3 ──┤ ──► Shared Memory Backend
     └─► API Instance N ──┘ ──► Shared Configuration
```

### **Performance Optimizations**
- **Vector Search**: Optimized similarity algorithms (HNSW, IVF)
- **Caching**: Multi-level caching (memory, distributed, CDN)
- **Connection Pooling**: Database connection reuse
- **Async Everywhere**: Non-blocking I/O operations
- **Token Budgeting**: Efficient LLM usage

---

## 🧪 **Testing Architecture**

### **Testing Pyramid**
```
                    ┌─────────────┐
                    │    E2E      │ ──► Full system tests
                    │   Tests     │
                    └─────────────┘
                ┌─────────────────────┐
                │   Integration       │ ──► API + Services
                │     Tests           │
                └─────────────────────┘
            ┌─────────────────────────────┐
            │        Unit Tests           │ ──► Individual components
            └─────────────────────────────┘
```

### **Test Strategy**
- **Unit Tests**: Domain entities and value objects
- **Integration Tests**: API endpoints with real dependencies
- **Component Tests**: Individual services and agents
- **Contract Tests**: API schema validation
- **Performance Tests**: Load and stress testing

---

## 🔍 **Quality Architecture**

### **Code Quality Gates**
```
Code Commit ──► Build ──► Test ──► Security Scan ──► Deploy
     │           │         │           │             │
     ▼           ▼         ▼           ▼             ▼
Linting     Compilation  Unit Tests  Vulnerability  Health
StyleCop    Warnings     Integration SAST/DAST      Checks
EditorConfig Errors      E2E Tests   Dependency     Monitoring
                                     Audit
```

### **Monitoring and Alerting**
- **SLI/SLO Definition**: Response time, availability, error rates
- **Alert Rules**: Performance degradation, error thresholds
- **Dashboard**: Real-time system health visualization
- **Incident Response**: Automated escalation procedures

---

## 🔮 **Future Architecture Considerations**

### **Planned Enhancements**
- **Multi-tenant Support**: Tenant isolation and data segregation
- **Advanced RAG**: Hybrid search, query rewriting, result fusion
- **Fine-tuning Pipeline**: Model customization for domain-specific tasks
- **Workflow Orchestration**: Complex multi-step AI workflows
- **Edge Deployment**: CDN-based inference for global users

### **Technology Evolution**
- **Vector Databases**: Adoption of specialized vector DBs
- **LLM Models**: Integration with latest foundation models
- **Embedding Models**: Domain-specific embedding fine-tuning
- **Infrastructure**: Serverless and edge computing adoption

---

## 📐 **Design Patterns Used**

- **Factory Pattern**: Vector store and service creation
- **Strategy Pattern**: Pluggable algorithm implementations
- **Repository Pattern**: Data access abstraction
- **Command Pattern**: Agent execution pipeline
- **Observer Pattern**: Event-driven telemetry
- **Decorator Pattern**: Middleware pipeline composition
- **Builder Pattern**: Configuration object construction

---

## 🎯 **Architecture Benefits**

### **Maintainability**
- Clear separation of concerns
- Minimal coupling between layers
- Consistent coding standards

### **Testability** 
- Dependency injection throughout
- Interface-based abstractions
- Isolated business logic

### **Flexibility**
- Pluggable components
- Configuration-driven behavior
- Multiple deployment options

### **Scalability**
- Stateless application design
- Horizontal scaling support
- Performance monitoring

### **Security**
- Defense in depth strategy
- Comprehensive audit trail
- Content safety integration

---

This architecture enables **OHS Copilot** to serve as a robust, enterprise-ready reference implementation for modern AI applications built on the Microsoft stack.
