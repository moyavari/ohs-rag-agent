# Troubleshooting Guide - OHS Copilot

## 🔧 **Quick Diagnostics**

### **Application Health Check**
```bash
# Check if application is running
curl -f http://localhost:5000/api/health

# Get detailed metrics
curl http://localhost:5000/api/metrics | jq .

# View recent audit logs
curl http://localhost:5000/api/audit-logs | jq '.auditLogs[:5]'
```

## 🚨 **Common Issues & Solutions**

### **Startup & Configuration Issues**

#### **❌ "Azure OpenAI Endpoint is required"**
```
Error: Azure OpenAI configuration is missing or invalid
```
**Solution**:
```bash
# Enable demo mode for testing
export DEMO_MODE=true

# OR provide real Azure OpenAI credentials
export AOAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AOAI_API_KEY="your-32-character-key"
export DEMO_MODE=false
```

#### **❌ "Cannot connect to vector store"**
```
Error: Vector store initialization failed
```
**Solutions**:
```bash
# For Qdrant connection issues
docker run -p 6333:6333 qdrant/qdrant:latest
export QDRANT_ENDPOINT="http://localhost:6333"

# For PostgreSQL issues  
docker run -p 5432:5432 -e POSTGRES_PASSWORD=pass pgvector/pgvector:latest
export PG_CONN_STR="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=pass"

# Fallback to JSON vector store
export VECTOR_STORE=json
```

#### **❌ "Cannot consume 500 tokens. Only 400 tokens remaining."**
```
Error: Token budget exceeded during agent execution
```
**Solution**:
```bash
# Increase max tokens per request
export MAX_TOKENS_PER_REQUEST=4096

# OR use demo mode which bypasses token budgeting
export DEMO_MODE=true
```

### **Runtime Issues**

#### **❌ API returning 500 errors**
```
Error: Internal Server Error on API requests
```
**Diagnostic Steps**:
```bash
# Check application logs
dotnet run --project src/OHS.Copilot.API 2>&1 | grep -i error

# Check vector store health
curl -X POST http://localhost:5000/api/test-vector-store

# Check dependency health
curl http://localhost:5000/api/health | jq .dependencies
```

#### **❌ Empty response bodies (200 but no content)**
```
Error: API returns 200 OK but response body is empty
```
**Solution**:
```bash
# This was a known issue with TelemetryMiddleware - should be fixed
# Verify middleware is properly handling response streams
curl -v http://localhost:5000/api/health | head -20

# If still occurring, disable telemetry temporarily
export TELEMETRY_ENABLED=false
```

#### **❌ Extremely slow response times**
```
Error: API requests taking >10 seconds to complete
```
**Diagnostic Steps**:
```bash
# Check system resources
top -p $(pgrep dotnet)

# Profile request performance
curl -w "@curl-format.txt" http://localhost:5000/api/health

# Check vector store performance
curl -X POST http://localhost:5000/api/test-vector-store -w "Total: %{time_total}s"
```

### **Docker & Container Issues**

#### **❌ "Permission denied" when using Docker**
```
Error: Permission denied while trying to connect to Docker daemon
```
**Solution**:
```bash
# Add user to docker group
sudo usermod -aG docker $USER

# Apply group changes
newgrp docker

# OR run with sudo (not recommended for production)
sudo docker-compose up -d
```

#### **❌ Docker Compose validation errors**
```
Error: Invalid docker-compose configuration
```
**Solution**:
```bash
# Use docker-compose (with hyphen) not docker compose
docker-compose --version

# Validate configuration
docker-compose config

# Check for syntax errors in YAML
yamllint docker-compose.override.yml
```

### **Azure Deployment Issues**

#### **❌ Bicep template validation fails**
```
Error: Template validation failed
```
**Solution**:
```bash
# Validate template locally
az bicep validate --file deployment/azure/main.bicep

# Check parameter file syntax
cat deployment/azure/parameters.json | jq .

# Verify Azure CLI version
az version | grep azure-cli
```

#### **❌ Container App deployment fails**
```
Error: Container deployment failed
```
**Diagnostic Steps**:
```bash
# Check deployment logs
az containerapp logs show \
  --name ohs-copilot-prod-api \
  --resource-group ohs-copilot-rg \
  --follow

# Check container registry access
az acr check-health --name yourregistry

# Verify image exists
az acr repository show-tags \
  --name yourregistry \
  --repository ohs-copilot
```

---

## 🔍 **Debugging Tools**

### **Application Debugging**

#### **Enable Debug Logging**
```bash
# Enable verbose logging
export Logging__LogLevel__Default=Debug
export Logging__LogLevel__OHS.Copilot=Trace

# Enable debug mode
export ASPNETCORE_ENVIRONMENT=Development
```

#### **Correlation ID Tracking**
```bash
# Use correlation IDs for request tracking
CORRELATION_ID=$(uuidgen)
curl -H "X-Correlation-ID: $CORRELATION_ID" \
  http://localhost:5000/api/ask

# Find logs for specific request
grep "$CORRELATION_ID" logs/application.log
```

### **Vector Store Debugging**

#### **Test Vector Store Connectivity**
```bash
# Test JSON vector store
ls -la fixtures/vectors.json
jq 'length' fixtures/vectors.json

# Test Qdrant
curl http://localhost:6333/collections

# Test PostgreSQL
psql -h localhost -U ohsuser -d ohscopilot -c "SELECT COUNT(*) FROM ohs_copilot.chunks;"

# Test Cosmos DB  
curl "https://your-cosmos.documents.azure.com/" \
  -H "Authorization: Bearer your-token"
```

#### **Vector Search Debugging**
```bash
# Test search functionality
curl -X POST http://localhost:5000/api/test-vector-store \
  -H "Content-Type: application/json" \
  -d '{"testQuery": "safety", "topK": 5}' | jq .
```

### **Agent Pipeline Debugging**

#### **Individual Agent Testing**
```bash
# Enable agent-level debugging
export AGENT_DEBUG=true

# Monitor agent execution times
curl -X POST http://localhost:5000/api/ask \
  -H "Content-Type: application/json" \
  -H "X-Debug-Mode: true" \
  -d '{"question": "test", "maxTokens": 1000}' | jq .metadata.agentResults
```

---

## 📊 **Performance Troubleshooting**

### **Memory Issues**

#### **High Memory Usage**
```bash
# Check .NET memory usage
dotnet-counters monitor --process-id $(pgrep dotnet) --counters System.Runtime

# Check vector store memory
curl http://localhost:5000/api/test-vector-store | jq .performance.memoryUsage

# Garbage collection analysis
export DOTNET_gcServer=1
export DOTNET_GCHeapAffinitizeMask=0xFF
```

#### **Memory Leaks**
```bash
# Monitor for leaks
dotnet-trace collect --process-id $(pgrep dotnet) --duration 00:01:00

# Analyze with PerfView or JetBrains dotMemory
```

### **CPU Performance Issues**

#### **High CPU Usage**
```bash
# Profile CPU usage
dotnet-counters monitor --process-id $(pgrep dotnet) --counters System.Runtime

# Identify hot paths
dotnet-trace collect --process-id $(pgrep dotnet) --providers Microsoft-DotNETCore-SampleProfiler
```

### **Database Performance**

#### **Slow Vector Searches**
```bash
# PostgreSQL optimization
EXPLAIN ANALYZE SELECT * FROM ohs_copilot.embeddings 
WHERE vector <=> '[0.1,0.2,...]' ORDER BY vector <=> '[0.1,0.2,...]' LIMIT 10;

# Check index usage
SELECT schemaname,tablename,indexname,idx_scan,idx_tup_read,idx_tup_fetch 
FROM pg_stat_user_indexes;
```

#### **Cosmos DB Performance Tuning**
```bash
# Monitor RU consumption
az cosmosdb collection throughput show \
  --account-name ohs-copilot-prod-cosmos \
  --resource-group ohs-copilot-rg \
  --database-name ohscopilot \
  --name chunks

# Optimize queries
# Enable query metrics in Azure portal
```

---

## 🔐 **Security Issues**

### **Authentication Problems**

#### **JWT Token Issues**
```bash
# Verify token format
echo "your-jwt-token" | cut -d. -f2 | base64 -d | jq .

# Check token expiry
export JWT_TOKEN="your-token"
export EXPIRY=$(echo $JWT_TOKEN | cut -d. -f2 | base64 -d | jq .exp)
date -d @$EXPIRY
```

#### **Key Vault Access Issues**
```bash
# Test managed identity
curl "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://vault.azure.net" \
  -H "Metadata: true"

# Verify Key Vault permissions
az keyvault secret list --vault-name ohs-copilot-prod-kv
```

### **Content Safety Issues**

#### **False Positives in Content Moderation**
```bash
# Check content safety thresholds
curl http://localhost:5000/api/health | jq .dependencies.contentSafety

# Adjust thresholds (if appropriate)
export CONTENT_SAFETY_THRESHOLD=Low  # High|Medium|Low
```

#### **PII Detection Issues**
```bash
# Test PII redaction
curl -X POST http://localhost:5000/api/ask \
  -d '{"question": "My SSN is 123-45-6789, what should I do?"}' | 
  jq .answer | grep -o "123-45-6789" || echo "PII properly redacted"
```

---

## 🌐 **Network & Connectivity**

### **DNS and Network Issues**

#### **Cannot reach external services**
```bash
# Test Azure OpenAI connectivity
curl -v "https://your-resource.openai.azure.com/openai/deployments/gpt-4/chat/completions?api-version=2023-12-01-preview" \
  -H "api-key: your-key" \
  -H "Content-Type: application/json"

# Test Cosmos DB connectivity
curl -v "https://your-cosmos.documents.azure.com/"

# Check DNS resolution
nslookup your-resource.openai.azure.com
```

#### **SSL/TLS Certificate Issues**
```bash
# Check certificate validity
echo | openssl s_client -servername your-app.azurecontainerapps.io -connect your-app.azurecontainerapps.io:443 2>/dev/null | openssl x509 -noout -dates

# Verify certificate chain
curl -vvI https://your-app.azurecontainerapps.io/api/health 2>&1 | grep -E "(certificate|SSL)"
```

---

## 📊 **Monitoring & Observability**

### **Telemetry Issues**

#### **OpenTelemetry Not Working**
```bash
# Check telemetry service status
curl http://localhost:5000/api/health | jq .dependencies.telemetry

# Verify exporters configuration
export TELEMETRY_ENABLED=true
export JAEGER_ENDPOINT="http://localhost:14268"
export PROMETHEUS_ENABLED=true
```

#### **Missing Metrics or Traces**
```bash
# Check Jaeger (if running locally)
curl http://localhost:16686/api/services

# Check Prometheus metrics  
curl http://localhost:9090/api/v1/query?query=http_request_total

# Check Application Insights (Azure)
# View in Azure Portal > Application Insights > Logs
```

### **Log Analysis**

#### **Structured Logging Queries**
```bash
# Find all errors in last hour
grep "$(date -d '1 hour ago' '+%Y-%m-%d %H')" logs/app.log | grep -i error

# Find requests by correlation ID
grep "correlation-id-here" logs/app.log

# Monitor real-time logs
tail -f logs/app.log | grep -E "(error|exception|fail)"
```

#### **Azure Log Analytics Queries**
```kusto
// Find errors in last 24 hours
traces
| where timestamp > ago(24h)
| where severityLevel >= 3
| order by timestamp desc

// Performance analysis
requests  
| where timestamp > ago(1h)
| summarize avg(duration), count() by name
| order by avg_duration desc

// User activity patterns
customEvents
| where name == "UserQuestion"
| summarize count() by bin(timestamp, 1h)
| render timechart
```

---

## ⚡ **Performance Troubleshooting**

### **Response Time Issues**

#### **Slow API Responses**
**Investigation Steps**:
1. **Check agent performance**:
   ```bash
   curl -X POST http://localhost:5000/api/ask \
     -H "X-Debug-Mode: true" \
     -d '{"question": "test"}' | jq .metadata.agentResults
   ```

2. **Profile vector search**:
   ```bash
   curl -X POST http://localhost:5000/api/test-vector-store \
     -d '{"testQuery": "safety", "topK": 10}' | jq .performance
   ```

3. **Check LLM response time**:
   ```bash
   # Monitor Azure OpenAI metrics in Azure Portal
   # OR check demo mode performance
   export DEMO_MODE=true
   ```

**Common Solutions**:
- **Reduce token limits**: Lower `maxTokens` in requests
- **Optimize vector search**: Reduce `topK` results
- **Enable caching**: Implement response caching for common queries
- **Scale resources**: Increase container CPU/memory allocation

#### **High Memory Usage**
```bash
# Check memory consumption
free -h
top -p $(pgrep dotnet)

# Clear vector store cache
rm -f fixtures/vectors.json
curl -X POST http://localhost:5000/api/test-vector-store  # Regenerates
```

### **Concurrency Issues**

#### **Request Timeouts Under Load**
```bash
# Test with limited concurrency
export MAX_CONCURRENT_REQUESTS=5

# Monitor request queuing
curl http://localhost:5000/api/metrics | jq .activeConnections
```

---

## 🔍 **Data Issues**

### **Vector Store Problems**

#### **No Search Results Returned**
**Investigation**:
```bash
# Check vector store initialization
curl -X POST http://localhost:5000/api/test-vector-store | jq .status

# Verify document ingestion
curl -X POST http://localhost:5000/api/ingest \
  -d '{"directoryOrZipPath": "/home/mohammad/ohs/data/seed"}' | jq .
```

**Solutions**:
- **Reingest documents**: Delete and recreate vector embeddings
- **Check embedding service**: Verify Azure OpenAI embedding model
- **Reset vector store**: Clear and reload fixture data

#### **Poor Search Relevance**
```bash
# Test search quality
curl -X POST http://localhost:5000/api/test-vector-store \
  -d '{"testQuery": "emergency evacuation", "topK": 5}' | jq '.searchResults[].score'

# Scores below 0.7 indicate relevance issues
```

**Solutions**:
- **Improve chunking**: Adjust chunk size and overlap parameters
- **Better embeddings**: Ensure proper text preprocessing
- **Query enhancement**: Implement query expansion or rewriting

### **Memory Persistence Issues**

#### **Conversation Context Lost**
```bash
# Test conversation persistence
CONV_ID="test-conversation-$(date +%s)"

# First message
curl -X POST http://localhost:5000/api/ask \
  -d "{\"question\": \"What are safety procedures?\", \"conversationId\": \"$CONV_ID\"}"

# Follow-up message
curl -X POST http://localhost:5000/api/ask \
  -d "{\"question\": \"Can you elaborate?\", \"conversationId\": \"$CONV_ID\"}"

# Check memory
curl http://localhost:5000/api/conversations/$CONV_ID | jq .messages
```

---

## 🔒 **Security Issues**

### **Content Safety Problems**

#### **Content Safety Service Unavailable**
```bash
# Check content safety status
curl http://localhost:5000/api/health | jq .dependencies.contentSafety

# Temporary workaround - disable content safety
export CONTENT_SAFETY_ENABLED=false
export REDACTION_ENABLED=false
```

#### **PII Redaction Not Working**
```bash
# Test PII detection
curl -X POST http://localhost:5000/api/ask \
  -d '{"question": "My email is test@example.com"}' | 
  grep "test@example.com" && echo "PII NOT REDACTED!" || echo "PII properly redacted"
```

### **Authentication Issues**

#### **API Key Validation Failing**
```bash
# Check API key format
echo $AOAI_API_KEY | wc -c  # Should be 32 characters

# Verify key validity
curl "https://your-resource.openai.azure.com/openai/deployments?api-version=2023-12-01-preview" \
  -H "api-key: $AOAI_API_KEY"
```

---

## 🛠️ **Development Environment Issues**

### **IDE and Tooling**

#### **.NET Build Issues**
```bash
# Clear build artifacts
dotnet clean
rm -rf bin/ obj/

# Restore packages
dotnet restore

# Check for package conflicts
dotnet list package --vulnerable --include-transitive
```

#### **Integration Test Failures**
```bash
# Run tests with verbose output
dotnet test tests/OHS.Copilot.IntegrationTests/ \
  --verbosity detailed \
  --logger "trx;LogFileName=test-results.trx"

# Check test environment
export DEMO_MODE=true
export VECTOR_STORE=json
dotnet test --filter "HealthTests"
```

### **Configuration Issues**

#### **Environment Variables Not Loading**
```bash
# Check environment variables
env | grep -E "(DEMO_MODE|VECTOR_STORE|AOAI_)"

# Verify configuration loading
curl http://localhost:5000/api/health | jq .

# Check configuration precedence
# 1. Environment variables (highest)
# 2. appsettings.{Environment}.json
# 3. appsettings.json (lowest)
```

---

## 📊 **Monitoring & Alerting**

### **Missing Telemetry Data**

#### **No Traces in Jaeger**
```bash
# Check Jaeger is running
curl http://localhost:16686/api/services

# Verify telemetry configuration
export JAEGER_ENDPOINT="http://localhost:14268"
export TELEMETRY_ENABLED=true

# Test trace generation
curl -X POST http://localhost:5000/api/ask \
  -d '{"question": "trace test"}' && \
  sleep 2 && \
  curl "http://localhost:16686/api/traces?service=OHS.Copilot.API"
```

#### **No Metrics in Prometheus**
```bash
# Check Prometheus is scraping
curl http://localhost:9090/api/v1/targets

# Verify metrics endpoint
curl http://localhost:5000/metrics | head -10

# Check Prometheus configuration
curl http://localhost:9090/api/v1/config | jq .
```

---

## 🚨 **Emergency Procedures**

### **System Recovery**

#### **Complete System Reset**
```bash
# Stop all services
pkill -f "dotnet run"
docker-compose down

# Clean state
rm -f fixtures/vectors.json
rm -rf logs/

# Restart in demo mode
export DEMO_MODE=true
./start-demo.sh
```

#### **Database Recovery**
```bash
# PostgreSQL recovery
docker-compose restart postgres
sleep 10
docker-compose exec postgres psql -U ohsuser -d ohscopilot -f /docker-entrypoint-initdb.d/01-init.sql

# Cosmos DB recovery
# Use Azure Portal > Cosmos DB > Backup and Restore
```

### **Production Incident Response**

#### **Azure Service Outage**
1. **Check Azure Status**: https://status.azure.com/
2. **Enable Demo Mode**: Temporary fallback for critical operations
3. **Notify Users**: Service degradation communication
4. **Monitor Recovery**: Track service restoration

#### **Security Incident**
1. **Immediate**: Block suspicious traffic via WAF rules
2. **Investigation**: Preserve logs and evidence
3. **Containment**: Isolate affected components
4. **Recovery**: Restore from known-good backup
5. **Post-incident**: Review and improve security controls

---

## 📞 **Support Resources**

### **Internal Support**
- **Development Team**: dev-team@company.com
- **Operations Team**: ops-team@company.com  
- **Security Team**: security@company.com
- **On-call Engineer**: +1-800-555-ONCALL

### **External Support**
- **Azure Support**: Create ticket via Azure Portal
- **Microsoft Semantic Kernel**: [GitHub Issues](https://github.com/microsoft/semantic-kernel/issues)
- **OpenTelemetry**: [Community Support](https://opentelemetry.io/community/)

### **Documentation Links**
- **Azure OpenAI**: https://docs.microsoft.com/azure/cognitive-services/openai/
- **Container Apps**: https://docs.microsoft.com/azure/container-apps/
- **Cosmos DB**: https://docs.microsoft.com/azure/cosmos-db/
- **Key Vault**: https://docs.microsoft.com/azure/key-vault/

---

## 🔧 **Diagnostic Scripts**

### **Health Check Script**
```bash
#!/bin/bash
# comprehensive-health-check.sh

echo "🔍 OHS Copilot Health Check"
echo "=========================="

# Application health
echo "1. Application Health:"
curl -f http://localhost:5000/api/health 2>/dev/null && echo "✅ API responding" || echo "❌ API not responding"

# Dependencies
echo "2. Dependencies:"
curl -s http://localhost:5000/api/health | jq -r '.dependencies | to_entries[] | "\(.key): \(.value)"' 2>/dev/null

# Performance
echo "3. Performance:" 
RESPONSE_TIME=$(curl -o /dev/null -s -w '%{time_total}' http://localhost:5000/api/health)
echo "Response time: ${RESPONSE_TIME}s"

# Vector store
echo "4. Vector Store:"
curl -X POST http://localhost:5000/api/test-vector-store 2>/dev/null | jq -r '.status' 2>/dev/null && echo "✅ Vector store healthy" || echo "❌ Vector store issues"

# Memory usage
echo "5. System Resources:"
free -h | head -2
ps aux | grep dotnet | head -1

echo "=========================="
echo "Health check completed"
```

### **Performance Benchmark Script**
```bash
#!/bin/bash
# performance-benchmark.sh

echo "🚀 OHS Copilot Performance Benchmark"
echo "=================================="

# Warm-up requests
echo "Warming up..."
for i in {1..5}; do
  curl -s -X POST http://localhost:5000/api/ask \
    -H "Content-Type: application/json" \
    -d '{"question": "warmup", "maxTokens": 500}' > /dev/null
done

# Benchmark requests
echo "Running benchmark (10 requests)..."
time for i in {1..10}; do
  curl -s -X POST http://localhost:5000/api/ask \
    -H "Content-Type: application/json" \
    -d '{"question": "What are safety procedures?", "maxTokens": 1000}' > /dev/null
done

# Get metrics
echo "Current Metrics:"
curl -s http://localhost:5000/api/metrics | jq '{totalRequests, averageResponseTime, errorRate}'

echo "=================================="
echo "Benchmark completed"
```

---

## 📋 **Troubleshooting Checklist**

### **Before Seeking Help**
- [ ] Checked application logs for errors
- [ ] Verified all environment variables are set
- [ ] Tested with demo mode to isolate issues
- [ ] Ran health check diagnostics
- [ ] Checked system resource usage
- [ ] Verified network connectivity
- [ ] Reviewed recent configuration changes

### **Information to Gather**
- **Error messages**: Exact error text and stack traces
- **Environment details**: OS, .NET version, deployment type
- **Configuration**: Relevant environment variables (sanitized)
- **Reproduction steps**: Minimal steps to reproduce the issue
- **Timing**: When the issue first occurred
- **Impact**: What functionality is affected

### **Escalation Criteria**
- **P1**: Application completely down, security breach, data loss
- **P2**: Major functionality broken, significant performance degradation
- **P3**: Minor functionality issues, intermittent problems
- **P4**: Enhancement requests, documentation issues

---

**This troubleshooting guide covers the most common issues and provides systematic approaches to diagnosis and resolution.**
