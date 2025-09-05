# API Reference Guide - OHS Copilot

## 🌐 **Base Information**

- **Base URL**: `http://localhost:5000` (local) or `https://your-app.azurecontainerapps.io` (Azure)
- **Content-Type**: `application/json`
- **Authentication**: Bearer token (optional, configurable)
- **Correlation**: Include `X-Correlation-ID` header for request tracking

## 📋 **Core API Endpoints**

### **Health & Monitoring**

#### `GET /api/health`
**Purpose**: Application health check

**Response**:
```json
{
  "ok": true,
  "status": "Healthy",
  "timestamp": "2025-01-04T12:00:00.000Z",
  "version": "1.0.0",
  "dependencies": {
    "vectorStore": "healthy",
    "memoryService": "healthy",
    "embeddingService": "healthy"
  }
}
```

#### `GET /api/metrics`
**Purpose**: Application performance metrics

**Response**:
```json
{
  "totalRequests": 1234,
  "averageResponseTime": 145.67,
  "errorRate": 0.02,
  "timestamp": "2025-01-04T12:00:00.000Z",
  "uptime": "72h 15m 30s",
  "activeConnections": 45
}
```

---

### **Question & Answer Pipeline**

#### `POST /api/ask`
**Purpose**: Ask questions using the RAG pipeline

**Request Body**:
```json
{
  "question": "What are the emergency evacuation procedures?",
  "conversationId": "conv-001",          // Optional: for multi-turn context
  "userId": "inspector-001",             // Optional: for persona-based responses
  "maxTokens": 2000,                     // Optional: defaults to 2000
  "includeMetadata": true                // Optional: include processing details
}
```

**Response**:
```json
{
  "answer": "Emergency evacuation procedures require all employees to...",
  "citations": [
    {
      "id": "c1",
      "score": 0.95,
      "title": "Emergency Procedures Manual",
      "url": "https://intranet.com/docs/emergency.pdf",
      "text": "All employees must familiarize themselves with evacuation routes..."
    }
  ],
  "metadata": {
    "processingTimeMs": 1250,
    "agentResults": {
      "router": { "intent": "ask", "confidence": 0.98 },
      "retriever": { "chunksFound": 5, "topScore": 0.95 },
      "drafter": { "tokensUsed": 1543 },
      "citeChecker": { "citationsValidated": 1, "safetyPassed": true }
    },
    "correlationId": "550e8400-e29b-41d4-a716-446655440000",
    "timestamp": "2025-01-04T12:00:00.000Z"
  }
}
```

**Error Response** (400/500):
```json
{
  "error": "Invalid request",
  "message": "Question cannot be empty",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-01-04T12:00:00.000Z"
}
```

---

### **Document Letter Drafting**

#### `POST /api/draft-letter`
**Purpose**: Generate formal correspondence

**Request Body**:
```json
{
  "purpose": "incident notification",
  "points": [
    "Investigation has been scheduled for next week",
    "All employees must complete safety training",
    "Additional documentation will be provided"
  ],
  "recipient": "Safety Department",          // Optional
  "tone": "formal",                         // Optional: formal|casual
  "maxTokens": 1500                         // Optional: defaults to 1500
}
```

**Response**:
```json
{
  "subject": "Incident Notification - Investigation Scheduled",
  "body": "Dear Safety Department,\n\nI am writing to inform you...",
  "metadata": {
    "processingTimeMs": 800,
    "wordCount": 156,
    "tokensUsed": 892,
    "correlationId": "550e8400-e29b-41d4-a716-446655440001",
    "timestamp": "2025-01-04T12:00:00.000Z"
  }
}
```

---

### **Document Processing**

#### `POST /api/ingest`
**Purpose**: Process and store documents for retrieval

**Request Body**:
```json
{
  "directoryOrZipPath": "/data/policies",
  "chunkSize": 1000,                      // Optional: defaults to 1000
  "chunkOverlap": 200,                    // Optional: defaults to 200
  "includeMetadata": true                 // Optional: processing details
}
```

**Response**:
```json
{
  "processedFiles": 15,
  "generatedChunks": 245,
  "processingTime": "00:00:05.234",
  "fileDetails": [
    {
      "fileName": "safety-manual.pdf",
      "chunks": 23,
      "status": "success"
    },
    {
      "fileName": "emergency-procedures.docx", 
      "chunks": 12,
      "status": "success"
    }
  ],
  "metadata": {
    "correlationId": "550e8400-e29b-41d4-a716-446655440002",
    "timestamp": "2025-01-04T12:00:00.000Z"
  }
}
```

---

## 🧠 **Memory Management**

### **Conversation History**

#### `GET /api/conversations/{conversationId}`
**Purpose**: Retrieve conversation history

**Response**:
```json
{
  "conversationId": "conv-001",
  "messages": [
    {
      "turnIndex": 1,
      "userMessage": "What are evacuation procedures?",
      "assistantMessage": "Emergency evacuation procedures require...",
      "timestamp": "2025-01-04T11:55:00.000Z",
      "metadata": {
        "processingTimeMs": 1200,
        "citationsCount": 2
      }
    }
  ],
  "totalMessages": 1,
  "createdAt": "2025-01-04T11:55:00.000Z",
  "lastUpdated": "2025-01-04T11:55:00.000Z"
}
```

### **User Personas**

#### `POST /api/personas/{userId}`
**Purpose**: Create or update user persona

**Request Body**:
```json
{
  "type": "Inspector",
  "preferences": [
    "detailed_responses",
    "regulatory_focus", 
    "citation_heavy"
  ],
  "description": "Senior Safety Inspector with 10 years experience"
}
```

#### `GET /api/personas/{userId}`
**Purpose**: Retrieve user persona

**Response**:
```json
{
  "userId": "inspector-001",
  "type": "Inspector", 
  "preferences": ["detailed_responses", "regulatory_focus"],
  "description": "Senior Safety Inspector with 10 years experience",
  "createdAt": "2025-01-04T10:00:00.000Z",
  "lastUpdated": "2025-01-04T11:30:00.000Z"
}
```

### **Policy Knowledge Base**

#### `GET /api/policies/search?q={query}&limit={limit}`
**Purpose**: Search organizational policies

**Query Parameters**:
- `q`: Search query string
- `limit`: Maximum results (default: 10)

**Response**:
```json
{
  "results": [
    {
      "id": "policy-001",
      "title": "Personal Protective Equipment Policy",
      "category": "Safety Equipment",
      "score": 0.92,
      "excerpt": "All workers in designated areas must wear...",
      "effectiveDate": "2024-01-01",
      "url": "/policies/ppe-policy.pdf"
    }
  ],
  "total": 1,
  "query": "PPE requirements",
  "processingTimeMs": 45
}
```

---

## 🔍 **Governance & Audit**

### **Audit Trail**

#### `GET /api/audit-logs?operation={operation}&userId={userId}&limit={limit}`
**Purpose**: Retrieve audit logs for compliance

**Query Parameters**:
- `operation`: Filter by operation type (ask, draft, ingest)
- `userId`: Filter by user ID
- `limit`: Maximum results (default: 50)

**Response**:
```json
{
  "auditLogs": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440003",
      "operation": "ask",
      "userId": "inspector-001", 
      "correlationId": "550e8400-e29b-41d4-a716-446655440000",
      "inputData": {
        "question": "What are chemical safety requirements?",
        "maxTokens": 2000
      },
      "outputData": {
        "answerLength": 456,
        "citationsCount": 3,
        "safetyCheckPassed": true
      },
      "processingTimeMs": 1250,
      "timestamp": "2025-01-04T12:00:00.000Z",
      "metadata": {
        "ipAddress": "192.168.1.100",
        "userAgent": "Mozilla/5.0...",
        "contentSafetyScore": 0.95
      }
    }
  ],
  "total": 1,
  "page": 1,
  "limit": 50
}
```

### **Prompt Versioning**

#### `GET /api/prompt-versions?promptName={name}`
**Purpose**: Retrieve prompt template versions

**Response**:
```json
{
  "promptVersions": [
    {
      "promptName": "ask_prompt",
      "versionHash": "abc123def456",
      "template": "You are a safety expert assistant...",
      "metadata": {
        "model": "gpt-4",
        "temperature": 0.1,
        "maxTokens": 2000
      },
      "createdAt": "2025-01-04T10:00:00.000Z"
    }
  ]
}
```

---

## 🎭 **Demo Mode & Evaluation**

### **Demo Fixtures**

#### `GET /api/demo-fixtures`
**Purpose**: Retrieve demo mode data for testing

**Response**:
```json
{
  "askFixtures": [
    {
      "question": "What are safety procedures?",
      "answer": "Demo safety response...",
      "citations": [...],
      "metadata": {...}
    }
  ],
  "letterFixtures": [
    {
      "purpose": "safety reminder",
      "subject": "Safety Training Reminder",
      "body": "This is to remind all employees...",
      "metadata": {...}
    }
  ]
}
```

### **Evaluation Framework**

#### `POST /api/evaluate`
**Purpose**: Run evaluation against golden dataset

**Request Body**:
```json
{
  "testCases": ["all"],                   // or specific test case IDs
  "includeDetails": true,                // Optional: detailed results
  "maxConcurrency": 5                    // Optional: parallel execution
}
```

**Response**:
```json
{
  "overallScore": 0.87,
  "testResults": [
    {
      "testCaseId": "tc-001",
      "question": "What are evacuation procedures?",
      "expectedAnswer": "All employees should...",
      "actualAnswer": "Emergency evacuation requires...", 
      "score": 0.92,
      "metrics": {
        "relevanceScore": 0.94,
        "accuracyScore": 0.90,
        "citationScore": 0.88
      }
    }
  ],
  "summary": {
    "totalTests": 20,
    "passed": 18,
    "failed": 2,
    "averageScore": 0.87,
    "processingTimeMs": 45678
  }
}
```

#### `GET /api/golden-dataset`
**Purpose**: Retrieve evaluation test cases

**Response**:
```json
{
  "testCases": [
    {
      "id": "tc-001",
      "question": "What are evacuation procedures?",
      "expectedAnswer": "All employees should proceed to...",
      "category": "emergency_response",
      "difficulty": "easy"
    }
  ],
  "total": 20
}
```

---

## 🔍 **Testing Endpoints**

### **Vector Store Testing**

#### `POST /api/test-vector-store`
**Purpose**: Test vector store connectivity and performance

**Request Body**:
```json
{
  "testQuery": "safety procedures",
  "topK": 5
}
```

**Response**:
```json
{
  "status": "healthy",
  "searchResults": [
    {
      "score": 0.95,
      "title": "Safety Manual - Chapter 3",
      "text": "Safety procedures must be followed..."
    }
  ],
  "performance": {
    "searchTimeMs": 45,
    "indexSize": 12456,
    "memoryUsage": "245MB"
  }
}
```

---

## 📊 **OpenTelemetry Integration**

### **Tracing Endpoints**

#### `GET /api/demo-traces/{traceId?}`
**Purpose**: Retrieve demo OpenTelemetry traces

**Response**:
```json
{
  "traces": [
    {
      "traceId": "demo-ask-trace-001",
      "spans": [
        {
          "name": "POST /api/ask",
          "startTime": "2025-01-04T12:00:00.000Z",
          "duration": "125ms",
          "attributes": {
            "http.method": "POST",
            "http.route": "/api/ask",
            "http.status_code": 200
          }
        }
      ]
    }
  ]
}
```

---

## 🚫 **Error Handling**

### **Standard Error Response Format**
```json
{
  "error": "ValidationError",
  "message": "Question cannot be empty",
  "details": {
    "field": "question",
    "value": "",
    "constraint": "required"
  },
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-01-04T12:00:00.000Z",
  "traceId": "abc123def456"
}
```

### **HTTP Status Codes**
| Code | Meaning | When |
|------|---------|------|
| `200` | OK | Successful request |
| `400` | Bad Request | Invalid input data |
| `401` | Unauthorized | Missing/invalid authentication |
| `403` | Forbidden | Insufficient permissions |
| `404` | Not Found | Resource doesn't exist |
| `429` | Too Many Requests | Rate limit exceeded |
| `500` | Internal Server Error | Application error |
| `503` | Service Unavailable | Dependencies unavailable |

---

## 🎛️ **Request/Response Headers**

### **Standard Headers**
- **Content-Type**: `application/json`
- **X-Correlation-ID**: Request correlation identifier
- **X-Request-ID**: Unique request identifier  
- **X-Processing-Time**: Server processing time in milliseconds
- **X-API-Version**: API version number

### **Security Headers**
- **X-Content-Type-Options**: `nosniff`
- **X-Frame-Options**: `DENY`
- **X-XSS-Protection**: `1; mode=block`
- **Strict-Transport-Security**: HTTPS enforcement

---

## 📝 **Request Examples**

### **Basic Question (cURL)**
```bash
curl -X POST http://localhost:5000/api/ask \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: $(uuidgen)" \
  -d '{
    "question": "What PPE is required in the workshop?",
    "maxTokens": 2000
  }' | jq .
```

### **Multi-turn Conversation (cURL)**
```bash
# First question
curl -X POST http://localhost:5000/api/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What are the evacuation procedures?",
    "conversationId": "safety-training-session"
  }' | jq .

# Follow-up question  
curl -X POST http://localhost:5000/api/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "How often should we practice these?",
    "conversationId": "safety-training-session"  
  }' | jq .
```

### **Document Ingestion (cURL)**
```bash
curl -X POST http://localhost:5000/api/ingest \
  -H "Content-Type: application/json" \
  -d '{
    "directoryOrZipPath": "/data/new-policies",
    "includeMetadata": true
  }' | jq .
```

### **Letter Drafting (cURL)**
```bash
curl -X POST http://localhost:5000/api/draft-letter \
  -H "Content-Type: application/json" \
  -d '{
    "purpose": "safety compliance reminder",
    "points": [
      "Monthly safety inspection due",
      "PPE compliance verification required", 
      "Training certificates need renewal"
    ],
    "tone": "formal"
  }' | jq .
```

---

## 🧪 **Development & Testing**

### **Demo Mode Testing**
When `DEMO_MODE=true`, the API returns deterministic fixture responses:

```bash
# All requests return demo responses
export DEMO_MODE=true

# Test with fixture responses
curl -X POST http://localhost:5000/api/ask \
  -d '{"question": "test"}' | jq .answer
# Returns: "This is a demo response..."
```

### **Integration Testing**
Use the provided integration test suite:

```bash
# Run full test suite
dotnet test tests/OHS.Copilot.IntegrationTests/

# Test specific functionality
dotnet test --filter "CoreApiTests"
dotnet test --filter "AgentTests"  
dotnet test --filter "GovernanceTests"
```

### **Performance Testing**
Monitor performance via metrics endpoint:

```bash
# Baseline metrics
curl http://localhost:5000/api/metrics | jq .averageResponseTime

# Load testing with Apache Bench
ab -n 100 -c 10 -H "Content-Type: application/json" \
  -p ask-request.json http://localhost:5000/api/ask

# Monitor with real-time metrics
watch -n 5 "curl -s http://localhost:5000/api/metrics | jq .averageResponseTime"
```

---

## 🔧 **Configuration via Headers**

### **Runtime Configuration**
Some behavior can be modified via request headers:

```bash
# Override max tokens per request
curl -X POST http://localhost:5000/api/ask \
  -H "X-Max-Tokens: 4000" \
  -d '{"question": "Detailed safety analysis"}' 

# Override vector search top-k
curl -X POST http://localhost:5000/api/ask \
  -H "X-Vector-Top-K: 15" \
  -d '{"question": "Comprehensive policy review"}'

# Include debug information
curl -X POST http://localhost:5000/api/ask \
  -H "X-Debug-Mode: true" \
  -d '{"question": "Debug this request"}'
```

---

## 📋 **OpenAPI Specification**

### **Swagger/OpenAPI**
Interactive API documentation available at:
- **Local**: http://localhost:5000/swagger (development only)
- **Schema**: http://localhost:5000/openapi/v1.json

### **Code Generation**
Generate client SDKs using the OpenAPI specification:

```bash
# Download OpenAPI spec
curl http://localhost:5000/openapi/v1.json > ohs-copilot-api.json

# Generate C# client
nswag openapi2csclient /input:ohs-copilot-api.json /output:OHSCopilotClient.cs

# Generate TypeScript client  
npx @openapitools/openapi-generator-cli generate \
  -i ohs-copilot-api.json \
  -g typescript-axios \
  -o ./clients/typescript
```

---

## 🛠️ **SDK Examples**

### **C# Client Example**
```csharp
using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

// Ask a question
var request = new AskRequest
{
    Question = "What are evacuation procedures?",
    MaxTokens = 2000,
    ConversationId = "safety-session"
};

var response = await httpClient.PostAsJsonAsync("/api/ask", request);
var answer = await response.Content.ReadFromJsonAsync<AskResponse>();

Console.WriteLine($"Answer: {answer.Answer}");
Console.WriteLine($"Citations: {answer.Citations.Count}");
```

### **Python Client Example**
```python
import requests
import json

# Configure client
base_url = "http://localhost:5000"
headers = {"Content-Type": "application/json"}

# Ask a question  
request_data = {
    "question": "What are emergency procedures?", 
    "maxTokens": 2000
}

response = requests.post(
    f"{base_url}/api/ask", 
    headers=headers,
    data=json.dumps(request_data)
)

if response.status_code == 200:
    result = response.json()
    print(f"Answer: {result['answer']}")
    print(f"Citations: {len(result['citations'])}")
else:
    print(f"Error: {response.status_code} - {response.text}")
```

### **JavaScript/Node.js Example**
```javascript
const axios = require('axios');

const client = axios.create({
    baseURL: 'http://localhost:5000',
    headers: { 'Content-Type': 'application/json' }
});

// Ask a question
async function askQuestion(question) {
    try {
        const response = await client.post('/api/ask', {
            question: question,
            maxTokens: 2000,
            includeMetadata: true
        });
        
        console.log(`Answer: ${response.data.answer}`);
        console.log(`Processing time: ${response.data.metadata.processingTimeMs}ms`);
        
        return response.data;
    } catch (error) {
        console.error(`Error: ${error.response?.status} - ${error.response?.data?.message}`);
    }
}

// Usage
askQuestion("What are the safety requirements?");
```

---

## 📊 **Rate Limits & Quotas**

### **Default Limits** 
| Endpoint | Rate Limit | Burst Limit | Window |
|----------|------------|-------------|---------|
| `/api/ask` | 30/min | 10/sec | Rolling |
| `/api/draft-letter` | 20/min | 5/sec | Rolling |
| `/api/ingest` | 5/min | 1/sec | Rolling |
| `/api/health` | 300/min | Unlimited | Rolling |

### **Rate Limit Headers**
```
X-RateLimit-Limit: 30
X-RateLimit-Remaining: 25
X-RateLimit-Reset: 1641024000
X-RateLimit-Window: 60
```

---

## 🔒 **Security Considerations**

### **Input Validation**
- All inputs validated against defined schemas
- Maximum length limits enforced
- SQL injection prevention
- XSS protection via encoding

### **Content Safety**
- Azure AI Content Safety integration
- PII redaction for sensitive data
- Prompt injection detection
- Response content filtering

### **Audit Requirements**
- Complete request/response logging
- User activity tracking
- Data access monitoring
- Compliance reporting

---

## 🚀 **Production Considerations**

### **Performance Optimization**
- Enable HTTP/2 and compression
- Configure connection pooling
- Implement response caching
- Use CDN for static assets

### **Monitoring Integration**
- Set up Application Insights alerts
- Configure Prometheus dashboards
- Enable distributed tracing
- Implement health check endpoints

### **Security Hardening**
- Use HTTPS only in production
- Implement API key authentication
- Configure WAF rules
- Enable audit logging

---

**This API provides a complete interface for enterprise AI applications with comprehensive safety, governance, and observability features.**
