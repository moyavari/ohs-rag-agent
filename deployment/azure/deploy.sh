#!/bin/bash

# Azure deployment script for OHS Copilot
set -e

# Configuration
RESOURCE_GROUP="ohs-copilot-rg"
LOCATION="eastus"
SUBSCRIPTION_ID="${AZURE_SUBSCRIPTION_ID}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}🚀 OHS Copilot Azure Deployment${NC}"
echo "================================"

# Check prerequisites
if ! command -v az &> /dev/null; then
    echo -e "${RED}❌ Azure CLI is required. Install from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli${NC}"
    exit 1
fi

# Login check
if ! az account show &> /dev/null; then
    echo -e "${YELLOW}📝 Please login to Azure CLI${NC}"
    az login
fi

# Set subscription
if [ -n "$SUBSCRIPTION_ID" ]; then
    echo -e "${YELLOW}📋 Setting subscription: $SUBSCRIPTION_ID${NC}"
    az account set --subscription "$SUBSCRIPTION_ID"
fi

# Create resource group
echo -e "${YELLOW}📂 Creating resource group: $RESOURCE_GROUP${NC}"
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --tags "Application=OHS Copilot" "Environment=Production" "ManagedBy=Azure CLI"

# Validate Bicep template
echo -e "${YELLOW}✅ Validating Bicep template${NC}"
az deployment group validate \
    --resource-group "$RESOURCE_GROUP" \
    --template-file main.bicep \
    --parameters @parameters.json

# Deploy infrastructure
echo -e "${YELLOW}🏗️ Deploying Azure infrastructure${NC}"
DEPLOYMENT_NAME="ohs-copilot-$(date +%Y%m%d-%H%M%S)"

az deployment group create \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file main.bicep \
    --parameters @parameters.json \
    --output table

# Get deployment outputs
echo -e "${YELLOW}📋 Getting deployment outputs${NC}"
API_URL=$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query 'properties.outputs.apiUrl.value' \
    --output tsv)

KEY_VAULT_NAME=$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query 'properties.outputs.keyVaultName.value' \
    --output tsv)

OPENAI_ENDPOINT=$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query 'properties.outputs.openAiEndpoint.value' \
    --output tsv)

echo -e "${GREEN}✅ Deployment completed successfully!${NC}"
echo "================================"
echo "🌐 API URL: $API_URL"
echo "🔑 Key Vault: $KEY_VAULT_NAME"
echo "🧠 OpenAI Endpoint: $OPENAI_ENDPOINT"
echo ""
echo -e "${GREEN}🔗 Useful links:${NC}"
echo "• Azure Portal: https://portal.azure.com/#@/resource/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP"
echo "• API Health Check: $API_URL/api/health"
echo "• OpenAPI Spec: $API_URL/openapi/v1.json"
echo ""
echo -e "${YELLOW}📝 Next steps:${NC}"
echo "1. Test the API: curl $API_URL/api/health"
echo "2. Upload documents: curl -X POST $API_URL/api/ingest -d '{\"directoryOrZipPath\":\"/data\"}'"
echo "3. Ask questions: curl -X POST $API_URL/api/ask -d '{\"question\":\"What are safety procedures?\"}'"
echo ""
echo -e "${GREEN}🎉 OHS Copilot is now running in Azure!${NC}"
