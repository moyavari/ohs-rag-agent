# Azure Deployment Guide - OHS Copilot

## 🚀 **Deployment Overview**

This guide provides step-by-step instructions for deploying OHS Copilot to Azure using Infrastructure as Code (Bicep templates).

## 📋 **Prerequisites**

### **Required Tools**
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) v2.50+
- [Azure PowerShell](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps) (alternative)
- [Git](https://git-scm.com/) for repository cloning
- [Docker](https://docker.com) for container builds (optional)

### **Azure Requirements**
- **Azure Subscription** with appropriate permissions
- **Resource Group** creation permissions
- **Azure OpenAI** service quota approved
- **Container Apps** environment permissions

### **Azure Service Quotas**
| Service | Quota Needed | Default Limit | Request If Needed |
|---------|--------------|---------------|-------------------|
| **Azure OpenAI** | GPT-4 Deployment | 0 (requires approval) | [Apply here](https://aka.ms/oai/access) |
| **Container Apps** | Cores | 20 | Usually sufficient |
| **Cosmos DB** | Request Units | 1000 RU/s | Usually sufficient |
| **Key Vault** | Vaults per region | 500 | Usually sufficient |

---

## 🔧 **One-Click Deployment**

### **Quick Deployment Script**
```bash
# Clone repository
git clone https://github.com/your-org/ohs-copilot.git
cd ohs-copilot

# Run deployment script
cd deployment/azure
chmod +x deploy.sh
./deploy.sh
```

The script will:
1. ✅ Validate Azure CLI login
2. ✅ Create resource group
3. ✅ Deploy all Azure resources
4. ✅ Configure secrets in Key Vault
5. ✅ Deploy and start the application
6. ✅ Verify deployment health

---

## 🎛️ **Manual Deployment Steps**

### **Step 1: Azure Login and Setup**
```bash
# Login to Azure
az login

# Set subscription (if you have multiple)
az account set --subscription "your-subscription-id"

# Register required providers
az provider register --namespace Microsoft.CognitiveServices
az provider register --namespace Microsoft.DocumentDB
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.KeyVault
```

### **Step 2: Create Resource Group**
```bash
# Create resource group
RESOURCE_GROUP="ohs-copilot-rg"
LOCATION="eastus"

az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION \
  --tags "Application=OHS Copilot" "Environment=Production"
```

### **Step 3: Deploy Infrastructure** 
```bash
# Navigate to deployment directory
cd deployment/azure

# Validate Bicep template
az deployment group validate \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters @parameters.json

# Deploy resources
az deployment group create \
  --name "ohs-copilot-$(date +%Y%m%d-%H%M%S)" \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters @parameters.json
```

### **Step 4: Configure Application Settings**
```bash
# Get deployment outputs
CONTAINER_APP_FQDN=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name "your-deployment-name" \
  --query 'properties.outputs.apiUrl.value' \
  --output tsv)

echo "Application deployed at: $CONTAINER_APP_FQDN"
```

### **Step 5: Verify Deployment**
```bash
# Test health endpoint
curl -f "$CONTAINER_APP_FQDN/api/health"

# Test basic functionality
curl -X POST "$CONTAINER_APP_FQDN/api/ask" \
  -H "Content-Type: application/json" \
  -d '{"question": "What are safety procedures?", "maxTokens": 2000}'
```

---

## 🏗️ **Infrastructure Components**

### **Core Azure Resources**

#### **Azure OpenAI Service**
```bash
# Resource configuration
Name: ohs-copilot-{environment}-openai
SKU: S0 (Standard)
Location: East US (best model availability)
Models: 
  - gpt-4 (10K TPM)
  - text-embedding-ada-002 (10K TPM)
```

#### **Azure Container Apps**
```bash
# Application configuration  
Name: ohs-copilot-{environment}-api
Environment: Managed Container Apps Environment
Scale: Min 1, Max 10 replicas
CPU: 1.0 vCPU per instance
Memory: 2.0 GB per instance
Ingress: External, HTTPS only, port 8080
```

#### **Azure Cosmos DB**
```bash
# Database configuration
Name: ohs-copilot-{environment}-cosmos
API: Core (SQL)
Capacity Mode: Serverless (dev) or Provisioned (prod)
Consistency: Session level
Geo-replication: Single region (dev) or Multi-region (prod)
```

#### **Azure Key Vault**
```bash
# Secrets management
Name: ohs-copilot-{environment}-kv
SKU: Standard
Access: RBAC-based
Secrets:
  - aoai-endpoint
  - aoai-api-key
  - cosmos-connection-string
```

### **Supporting Services**

#### **Application Insights**
```bash
# Monitoring configuration
Name: ohs-copilot-{environment}-appinsights
Type: Web application
Workspace: Dedicated Log Analytics workspace
Retention: 90 days (configurable)
```

#### **Managed Identity**
```bash
# Security configuration
Name: ohs-copilot-{environment}-identity
Type: User-assigned managed identity
Permissions:
  - Key Vault Secrets User (Key Vault)
  - Cognitive Services User (Azure OpenAI)
  - DocumentDB Account Contributor (Cosmos DB)
```

---

## 🔧 **Environment Configuration**

### **Development Environment**
```json
{
  "environmentName": "dev",
  "features": {
    "demoModeEnabled": true,
    "verboseLogging": true,
    "debugTelemetry": true
  },
  "scaling": {
    "minInstances": 1,
    "maxInstances": 3
  },
  "resources": {
    "cosmosFreeTier": true,
    "applicationInsightsRetention": 30
  }
}
```

### **Production Environment**
```json
{
  "environmentName": "prod",
  "features": {
    "demoModeEnabled": false,
    "contentSafetyEnabled": true,
    "auditLoggingEnabled": true
  },
  "scaling": {
    "minInstances": 2,
    "maxInstances": 20
  },
  "resources": {
    "cosmosProvisionedThroughput": 10000,
    "applicationInsightsRetention": 90,
    "geoRedundancy": true
  }
}
```

---

## 🔒 **Security Configuration**

### **Network Security**
```bash
# Create private networking (optional for high security)
az network vnet create \
  --resource-group $RESOURCE_GROUP \
  --name ohs-copilot-vnet \
  --address-prefix 10.0.0.0/16

az network subnet create \
  --resource-group $RESOURCE_GROUP \
  --vnet-name ohs-copilot-vnet \
  --name container-apps-subnet \
  --address-prefix 10.0.1.0/24

# Configure private endpoints for Cosmos DB
az cosmosdb private-endpoint-connection create \
  --account-name ohs-copilot-prod-cosmos \
  --resource-group $RESOURCE_GROUP \
  --subnet container-apps-subnet
```

### **Identity and Access Management**
```bash
# Create managed identity
az identity create \
  --name ohs-copilot-prod-identity \
  --resource-group $RESOURCE_GROUP

# Assign Key Vault access
az keyvault set-policy \
  --name ohs-copilot-prod-kv \
  --object-id $(az identity show --name ohs-copilot-prod-identity --resource-group $RESOURCE_GROUP --query principalId -o tsv) \
  --secret-permissions get list

# Assign Azure OpenAI access
az role assignment create \
  --assignee $(az identity show --name ohs-copilot-prod-identity --resource-group $RESOURCE_GROUP --query principalId -o tsv) \
  --role "Cognitive Services User" \
  --scope $(az cognitiveservices account show --name ohs-copilot-prod-openai --resource-group $RESOURCE_GROUP --query id -o tsv)
```

---

## 📊 **Monitoring Setup**

### **Application Insights Configuration**
```bash
# Configure custom metrics
az monitor app-insights component create \
  --app ohs-copilot-prod-appinsights \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --workspace $(az monitor log-analytics workspace show --name ohs-copilot-prod-logs --resource-group $RESOURCE_GROUP --query id -o tsv)

# Set up alerts
az monitor metrics alert create \
  --name "High Error Rate" \
  --resource-group $RESOURCE_GROUP \
  --scopes $(az monitor app-insights component show --app ohs-copilot-prod-appinsights --resource-group $RESOURCE_GROUP --query id -o tsv) \
  --condition "avg requests/failed > 5%" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action-groups security-team@company.com
```

### **Dashboard Setup**
```bash
# Import pre-built dashboard
az portal dashboard import \
  --input-path "./monitoring/azure-dashboard.json" \
  --resource-group $RESOURCE_GROUP
```

---

## 🔄 **CI/CD Pipeline Setup**

### **GitHub Actions Configuration**

#### **Required Secrets**
Configure these secrets in your GitHub repository:
```
AZURE_CREDENTIALS_PROD = {
  "clientId": "service-principal-client-id",
  "clientSecret": "service-principal-secret", 
  "subscriptionId": "azure-subscription-id",
  "tenantId": "azure-tenant-id"
}

AZURE_RG_PROD = "ohs-copilot-rg"
CONTAINER_REGISTRY = "ghcr.io"
```

#### **Deployment Pipeline**
The included `.github/workflows/ci-cd.yml` provides:
- ✅ Automated building and testing
- ✅ Security vulnerability scanning
- ✅ Container image building and pushing
- ✅ Azure infrastructure deployment
- ✅ Application health verification

### **Manual Container Deployment**
```bash
# Build container locally
docker build -t ohs-copilot:latest .

# Tag for Azure Container Registry
docker tag ohs-copilot:latest myregistry.azurecr.io/ohs-copilot:latest

# Push to registry
az acr login --name myregistry
docker push myregistry.azurecr.io/ohs-copilot:latest

# Update Container App
az containerapp update \
  --name ohs-copilot-prod-api \
  --resource-group $RESOURCE_GROUP \
  --image myregistry.azurecr.io/ohs-copilot:latest
```

---

## 🎛️ **Configuration Management**

### **Environment Variables in Azure**
```bash
# Configure application settings
az containerapp update \
  --name ohs-copilot-prod-api \
  --resource-group $RESOURCE_GROUP \
  --set-env-vars \
    DEMO_MODE=false \
    VECTOR_STORE=cosmos \
    MEMORY_BACKEND=cosmos \
    TELEMETRY_ENABLED=true \
    CONTENT_SAFETY_ENABLED=true
```

### **Key Vault Secrets Management**
```bash
# Store secrets in Key Vault
az keyvault secret set \
  --vault-name ohs-copilot-prod-kv \
  --name aoai-endpoint \
  --value "https://ohs-copilot-prod-openai.openai.azure.com/"

az keyvault secret set \
  --vault-name ohs-copilot-prod-kv \
  --name aoai-api-key \
  --value "your-api-key-here"

# Reference secrets in Container App
az containerapp update \
  --name ohs-copilot-prod-api \
  --resource-group $RESOURCE_GROUP \
  --secrets aoai-endpoint=keyvaultref:https://ohs-copilot-prod-kv.vault.azure.net/secrets/aoai-endpoint,identityref:/subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/ohs-copilot-prod-identity
```

---

## 🔍 **Post-Deployment Validation**

### **Health Check Verification**
```bash
# Basic health check
curl -f "https://your-app.azurecontainerapps.io/api/health"

# Detailed system check
curl "https://your-app.azurecontainerapps.io/api/metrics" | jq .

# Test core functionality
curl -X POST "https://your-app.azurecontainerapps.io/api/ask" \
  -H "Content-Type: application/json" \
  -d '{"question": "Test deployment", "maxTokens": 1000}'
```

### **Performance Testing**
```bash
# Load testing with Apache Bench
ab -n 100 -c 5 -H "Content-Type: application/json" \
  -p test-request.json \
  https://your-app.azurecontainerapps.io/api/ask

# Monitor during load test
curl "https://your-app.azurecontainerapps.io/api/metrics" | jq '.averageResponseTime'
```

### **Security Validation**
```bash
# SSL/TLS verification
curl -I "https://your-app.azurecontainerapps.io/api/health" | grep "strict-transport-security"

# Security headers check  
curl -I "https://your-app.azurecontainerapps.io/api/health" | grep -E "(x-content-type-options|x-frame-options|x-xss-protection)"

# Authentication test (if enabled)
curl -f "https://your-app.azurecontainerapps.io/api/ask" \
  -H "Authorization: Bearer invalid-token"
# Should return 401 Unauthorized
```

---

## 🛠️ **Troubleshooting Common Issues**

### **Deployment Failures**

#### **Issue: Azure OpenAI Quota Exceeded**
```
Error: "Insufficient quota for gpt-4 deployment"
```
**Solution**:
```bash
# Check current quotas
az cognitiveservices account list-usage \
  --name ohs-copilot-prod-openai \
  --resource-group $RESOURCE_GROUP

# Request quota increase at: https://aka.ms/oai/quotaincrease
```

#### **Issue: Container App Startup Failure**
```
Error: "Container failed to start"
```
**Solution**:
```bash
# Check container logs
az containerapp logs show \
  --name ohs-copilot-prod-api \
  --resource-group $RESOURCE_GROUP \
  --follow

# Common issues:
# - Missing environment variables
# - Invalid Azure OpenAI credentials
# - Key Vault access denied
```

#### **Issue: Key Vault Access Denied**
```
Error: "Access denied to Key Vault"
```
**Solution**:
```bash
# Verify managed identity permissions
az role assignment list \
  --assignee $(az identity show --name ohs-copilot-prod-identity --resource-group $RESOURCE_GROUP --query principalId -o tsv) \
  --output table

# Add missing permission
az keyvault set-policy \
  --name ohs-copilot-prod-kv \
  --object-id $(az identity show --name ohs-copilot-prod-identity --resource-group $RESOURCE_GROUP --query principalId -o tsv) \
  --secret-permissions get list
```

### **Application Issues**

#### **Issue: Vector Store Initialization Failure**
```
Error: "Cannot connect to Cosmos DB"
```
**Solution**:
```bash
# Check Cosmos DB connection string
az cosmosdb keys list \
  --name ohs-copilot-prod-cosmos \
  --resource-group $RESOURCE_GROUP

# Verify network connectivity
az containerapp exec \
  --name ohs-copilot-prod-api \
  --resource-group $RESOURCE_GROUP \
  --command "curl -v https://ohs-copilot-prod-cosmos.documents.azure.com/"
```

#### **Issue: Azure OpenAI Rate Limiting**
```
Error: "Rate limit exceeded (429)"
```
**Solution**:
```bash
# Check current usage
az cognitiveservices account list-usage \
  --name ohs-copilot-prod-openai \
  --resource-group $RESOURCE_GROUP

# Scale up deployment capacity
az cognitiveservices account deployment update \
  --name ohs-copilot-prod-openai \
  --resource-group $RESOURCE_GROUP \
  --deployment-name gpt-4 \
  --capacity 20
```

---

## 📊 **Scaling Configuration**

### **Auto-scaling Settings**
```bash
# Configure HTTP-based scaling
az containerapp update \
  --name ohs-copilot-prod-api \
  --resource-group $RESOURCE_GROUP \
  --min-replicas 2 \
  --max-replicas 20 \
  --scale-rule-name "http-requests" \
  --scale-rule-type "http" \
  --scale-rule-metadata "concurrentRequests=50"

# Configure CPU-based scaling
az containerapp update \
  --name ohs-copilot-prod-api \
  --resource-group $RESOURCE_GROUP \
  --scale-rule-name "cpu-usage" \
  --scale-rule-type "cpu" \
  --scale-rule-metadata "cpuUtilization=70"
```

### **Resource Optimization**
```bash
# Monitor resource usage
az containerapp show \
  --name ohs-copilot-prod-api \
  --resource-group $RESOURCE_GROUP \
  --query "properties.template.containers[0].resources"

# Update resource allocation
az containerapp update \
  --name ohs-copilot-prod-api \
  --resource-group $RESOURCE_GROUP \
  --cpu 2.0 \
  --memory 4.0Gi
```

---

## 🔍 **Monitoring & Alerts**

### **Key Performance Indicators**
- **Response Time**: P95 < 3 seconds
- **Error Rate**: < 1% of requests
- **Availability**: > 99.9% uptime
- **Token Usage**: < 80% of quota

### **Alert Configuration**
```bash
# High error rate alert
az monitor metrics alert create \
  --name "High Error Rate" \
  --resource-group $RESOURCE_GROUP \
  --scopes $(az containerapp show --name ohs-copilot-prod-api --resource-group $RESOURCE_GROUP --query id -o tsv) \
  --condition "avg failed_requests > 5" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --severity 1 \
  --description "Application error rate exceeded 5%"

# Resource utilization alert  
az monitor metrics alert create \
  --name "High CPU Usage" \
  --resource-group $RESOURCE_GROUP \
  --scopes $(az containerapp show --name ohs-copilot-prod-api --resource-group $RESOURCE_GROUP --query id -o tsv) \
  --condition "avg cpu_percentage > 80" \
  --window-size 10m \
  --evaluation-frequency 5m \
  --severity 2 \
  --description "CPU usage consistently above 80%"
```

### **Dashboard Setup**
```bash
# Create monitoring dashboard
az portal dashboard create \
  --resource-group $RESOURCE_GROUP \
  --name "OHS Copilot Monitoring" \
  --input-path "./monitoring/azure-dashboard.json"
```

---

## 💰 **Cost Management**

### **Cost Optimization Strategies**

#### **Azure OpenAI Optimization**
- **Token Management**: Implement strict token budgets per request
- **Caching**: Cache responses for common questions (respecting privacy)
- **Model Selection**: Use appropriate models (GPT-3.5 for simple queries)
- **Quota Monitoring**: Set up billing alerts for usage thresholds

#### **Cosmos DB Optimization**
- **Serverless Mode**: Use for development and low-traffic scenarios
- **Provisioned Mode**: Optimize RU/s allocation based on usage patterns
- **TTL Policies**: Set time-to-live for temporary data
- **Indexing Strategy**: Optimize indexes to reduce RU consumption

#### **Container Apps Optimization**
- **Right-sizing**: Monitor CPU/memory usage and adjust accordingly
- **Scale-to-zero**: Configure minimum replicas based on SLA requirements
- **Reserved Instances**: Use Azure Reserved Capacity for predictable workloads

### **Cost Monitoring**
```bash
# Set up budget alerts
az consumption budget create \
  --budget-name "OHS Copilot Monthly Budget" \
  --amount 500 \
  --resource-group $RESOURCE_GROUP \
  --time-grain Monthly \
  --threshold 80 \
  --contact-emails billing@company.com
```

---

## 🔄 **Backup & Disaster Recovery**

### **Backup Strategy**
```bash
# Cosmos DB continuous backup
az cosmosdb update \
  --name ohs-copilot-prod-cosmos \
  --resource-group $RESOURCE_GROUP \
  --backup-policy-type Continuous \
  --backup-retention-in-hours 168  # 7 days
```

### **Disaster Recovery Setup**
```bash
# Create secondary region deployment
SECONDARY_REGION="westus2"
SECONDARY_RG="ohs-copilot-dr-rg"

# Deploy to secondary region with read replicas
az group create --name $SECONDARY_RG --location $SECONDARY_REGION

az deployment group create \
  --name "ohs-copilot-dr-$(date +%Y%m%d)" \
  --resource-group $SECONDARY_RG \
  --template-file main.bicep \
  --parameters @parameters.dr.json \
  --parameters location=$SECONDARY_REGION
```

### **Recovery Procedures**
1. **Data Recovery**: Point-in-time restore from Cosmos DB backup
2. **Application Recovery**: Deploy from container registry to DR region
3. **DNS Failover**: Update DNS to point to DR environment
4. **Validation**: Run health checks and basic functionality tests

---

## 📋 **Deployment Checklist**

### **Pre-Deployment**
- [ ] Azure subscription with required quotas
- [ ] Azure CLI installed and authenticated
- [ ] Repository cloned with latest code
- [ ] Environment-specific parameters configured
- [ ] Security review completed
- [ ] Backup strategy defined

### **Deployment Execution**
- [ ] Resource group created
- [ ] Bicep template validation passed
- [ ] Infrastructure deployment successful
- [ ] Secrets stored in Key Vault
- [ ] Managed identity permissions configured
- [ ] Container app deployed and healthy

### **Post-Deployment**
- [ ] Health checks passing
- [ ] All API endpoints responding
- [ ] Performance tests completed
- [ ] Security scans passed
- [ ] Monitoring and alerting configured
- [ ] Documentation updated
- [ ] Team trained on new environment

---

## 🎯 **Production Readiness**

### **Operational Excellence**
- **Monitoring**: Comprehensive telemetry with Application Insights
- **Alerting**: Proactive notifications for issues
- **Logging**: Centralized log collection with retention policies
- **Backup**: Automated backup with tested recovery procedures

### **Security Excellence**
- **Identity**: Managed identities with least privilege access
- **Secrets**: Key Vault integration with rotation policies
- **Network**: Private endpoints and security groups
- **Compliance**: Full audit trail and governance controls

### **Performance Excellence**
- **Scalability**: Auto-scaling based on demand
- **Performance Optimization**: Resource allocation tuned for workload
- **Caching**: Multi-layer caching strategy
- **CDN**: Global content delivery for static assets

---

## 📞 **Support & Escalation**

### **Support Contacts**
- **Technical Issues**: devops-team@company.com
- **Security Incidents**: security-team@company.com  
- **Business Continuity**: operations-manager@company.com
- **Azure Support**: Create support case via Azure Portal

### **Escalation Matrix**
| Severity | Response Time | Escalation | Contact |
|----------|---------------|------------|---------|
| **P1 - Critical** | 15 minutes | Immediate | On-call engineer |
| **P2 - High** | 2 hours | Manager | Team lead |
| **P3 - Medium** | 8 hours | Standard | Assigned engineer |
| **P4 - Low** | 72 hours | Standard | Team queue |

---

## 🎉 **Successful Deployment Indicators**

### **Technical Success**
- ✅ All health checks passing
- ✅ Response times within SLA (<3s P95)
- ✅ Error rates minimal (<1%)
- ✅ Security scans clean
- ✅ Monitoring data flowing

### **Business Success**  
- ✅ End-users can access application
- ✅ Core functionality working (ask, draft, ingest)
- ✅ Quality metrics meeting targets
- ✅ Compliance requirements satisfied
- ✅ Cost within budget projections

---

**Following this guide ensures a successful, secure, and scalable deployment of OHS Copilot in Azure with enterprise-grade operational practices.**
