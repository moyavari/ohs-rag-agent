#!/bin/bash
set -euo pipefail  # Exit on error, undefined vars, pipe failures

# OHS Copilot Quick Start
echo "🚀 OHS Copilot Quick Start"
echo "====================================="

RESOURCE_NAME="ohs-copilot-openai"
RESOURCE_GROUP="AI-OHS"
COSMOS_NAME="ohsvectors1757056471"

# Stop everything first
echo "🛑 Stopping any running services..."
./stop-all.sh > /dev/null 2>&1

echo "✅ Azure OpenAI Resource: $RESOURCE_NAME"

# Deploy required models (only if they don't exist)
echo "📦 Ensuring models are deployed..."

# Check if models exist, deploy if needed
if ! az cognitiveservices account deployment show --name "$RESOURCE_NAME" --resource-group "$RESOURCE_GROUP" --deployment-name "gpt-4" > /dev/null 2>&1; then
    echo "🔄 Deploying GPT-4..."
    az cognitiveservices account deployment create \
      --name "$RESOURCE_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --deployment-name "gpt-4" \
      --model-name "gpt-4" \
      --model-version "turbo-2024-04-09" \
      --model-format "OpenAI" \
      --sku-capacity 1 \
      --sku-name "Standard" > /dev/null 2>&1
else
    echo "✅ GPT-4 already deployed"
fi

if ! az cognitiveservices account deployment show --name "$RESOURCE_NAME" --resource-group "$RESOURCE_GROUP" --deployment-name "text-embedding-ada-002" > /dev/null 2>&1; then
    echo "🔄 Deploying text-embedding-ada-002..."
    az cognitiveservices account deployment create \
      --name "$RESOURCE_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --deployment-name "text-embedding-ada-002" \
      --model-name "text-embedding-ada-002" \
      --model-version "2" \
      --model-format "OpenAI" \
      --sku-capacity 1 \
      --sku-name "Standard" > /dev/null 2>&1
else
    echo "✅ text-embedding-ada-002 already deployed"
fi

# Create Cosmos DB if it doesn't exist
echo "📦 Ensuring Cosmos DB is created..."
if ! az cosmosdb show --name "$COSMOS_NAME" --resource-group "$RESOURCE_GROUP" > /dev/null 2>&1; then
    echo "🔄 Creating Cosmos DB..."
    az cosmosdb create \
      --name "$COSMOS_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --kind GlobalDocumentDB \
      --locations regionName="West Europe" failoverPriority=0 isZoneRedundant=False \
      --default-consistency-level "Session" \
      --enable-automatic-failover true \
      --enable-multiple-write-locations true > /dev/null 2>&1
else
    echo "✅ Cosmos DB already exists"
fi

# Get credentials with error checking
echo "🔑 Retrieving credentials..."
ENDPOINT=$(az cognitiveservices account show --name "$RESOURCE_NAME" --resource-group "$RESOURCE_GROUP" --query "properties.endpoint" -o tsv)
if [ -z "$ENDPOINT" ]; then
    echo "❌ Error: Failed to get OpenAI endpoint"
    exit 1
fi

API_KEY=$(az cognitiveservices account keys list --name "$RESOURCE_NAME" --resource-group "$RESOURCE_GROUP" --query "key1" -o tsv)
if [ -z "$API_KEY" ]; then
    echo "❌ Error: Failed to get OpenAI API key"
    exit 1
fi

COSMOS_CONN_STR=$(az cosmosdb keys list --name "$COSMOS_NAME" --resource-group "$RESOURCE_GROUP" --type connection-strings --query "connectionStrings[0].connectionString" -o tsv)
if [ -z "$COSMOS_CONN_STR" ]; then
    echo "❌ Error: Failed to get Cosmos DB connection string"
    exit 1
fi

echo ""
echo "🔧 Azure Configuration:"
echo "• OpenAI Resource: $RESOURCE_NAME"
echo "• OpenAI Endpoint: $ENDPOINT"
echo "• OpenAI API Key: ${API_KEY:0:8}..."
echo "• Cosmos DB: $COSMOS_NAME"
echo "• Cosmos Connection: ${COSMOS_CONN_STR:0:50}..."
echo "• Demo Mode: false (real Azure services)"
echo "• Vector Store: cosmos (real Azure Cosmos DB)"
echo ""

# Create logs directory
mkdir -p logs

# Start API with explicit environment variables
echo "🚀 Starting API with Azure OpenAI and Cosmos DB..."
cd src/OHS.Copilot.API

# Use env to pass variables directly to the process
env DEMO_MODE=false \
    VECTOR_STORE=cosmos \
    AOAI_ENDPOINT="$ENDPOINT" \
    AOAI_API_KEY="$API_KEY" \
    AOAI_CHAT_DEPLOYMENT="gpt-4" \
    AOAI_EMB_DEPLOYMENT="text-embedding-ada-002" \
    MEMORY_BACKEND=cosmos \
    COSMOS_CONN_STR="$COSMOS_CONN_STR" \
    ASPNETCORE_ENVIRONMENT=Development \
    dotnet run --urls "http://localhost:5000" > ../../logs/api.log 2>&1 &
API_PID=$!
cd ../..

# Wait for API to start
echo "⏳ Waiting for API to start..."
for i in {1..30}; do
    if curl -s http://localhost:5000/api/health > /dev/null 2>&1; then
        echo "✅ API started successfully"
        
        # Verify vector store configuration
        VECTOR_STATUS=$(curl -s http://localhost:5000/api/health | jq -r '.dependencies.vectorStore.status // "unknown"')
        if [ "$VECTOR_STATUS" != "cosmos" ]; then
            echo "⚠️  Warning: Vector store is '$VECTOR_STATUS' instead of 'cosmos'"
        else
            echo "✅ Vector store correctly configured as 'cosmos'"
        fi
        break
    fi
    sleep 1
    if [ $i -eq 30 ]; then
        echo "❌ API failed to start. Check logs/api.log"
        kill $API_PID 2>/dev/null
        exit 1
    fi
done

# Start frontend
echo "🌐 Starting frontend..."
cd frontend
echo "Frontend available at: http://localhost:8080"

# Open browser
if command -v google-chrome > /dev/null 2>&1; then
    echo "🌐 Opening in Chrome..."
    google-chrome "http://localhost:8080" > /dev/null 2>&1 &
fi

echo ""
echo "🎉 Azure OpenAI + Cosmos DB Demo Ready!"
echo "========================================"
echo ""
echo "📋 Next Steps:"
echo "1️⃣  Add knowledge base:"
echo "   ./add-knowledge-base.sh"
echo ""
echo "2️⃣  Ask questions via frontend:"
echo "   • What are the safety procedures?"
echo "   • How do I report an incident?"
echo ""
echo "3️⃣  Draft letters with AI assistance"
echo ""
echo "🔧 Using Real Azure Services:"
echo "   • OpenAI: $RESOURCE_NAME"
echo "   • Cosmos DB: $COSMOS_NAME"
echo "   • Vector Storage: Real Cosmos DB (not emulator)"
echo ""
echo "🛑 To stop: Press Ctrl+C or run ./stop-all.sh"
echo "📊 API logs: tail -f logs/api.log"
echo ""

# Start frontend server
python3 -m http.server 8080

# Cleanup on exit
trap "echo ''; echo '🛑 Stopping demo...'; kill $API_PID 2>/dev/null; echo '✅ Demo stopped'; exit 0" EXIT
