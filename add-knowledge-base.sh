#!/bin/bash

# Add Knowledge Base to OHS Copilot
echo "📚 OHS Copilot Knowledge Base Manager"
echo "====================================="

# Check if API is running
if ! curl -s http://localhost:5000/api/health > /dev/null 2>&1; then
    echo "❌ API is not running!"
    echo "Please start the demo first: ./start-demo-openai.sh"
    exit 1
fi

echo "✅ API is running"
echo ""

if [ "$1" == "list" ]; then
    echo "📋 Available sample documents:"
    echo ""
    if [ -d "data/seed" ]; then
        find data/seed -name "*.md" -o -name "*.txt" -o -name "*.pdf" | while read file; do
            echo "  📄 $file"
        done
    else
        echo "  No sample documents found in data/seed/"
    fi
    echo ""
    echo "💡 Usage examples:"
    echo "  ./add-knowledge-base.sh                    # Ingest all sample documents"
    echo "  ./add-knowledge-base.sh /path/to/document  # Ingest specific file/directory"
    echo "  ./add-knowledge-base.sh list               # Show available documents"
    exit 0
fi

# Determine what to ingest
INGEST_PATH="$(pwd)/data/seed"
if [ -n "$1" ]; then
    if [[ "$1" = /* ]]; then
        INGEST_PATH="$1"
    else
        INGEST_PATH="$(pwd)/$1"
    fi
fi

if [ ! -e "$INGEST_PATH" ]; then
    echo "❌ Path does not exist: $INGEST_PATH"
    echo ""
    echo "💡 Available options:"
    echo "  ./add-knowledge-base.sh list                   # Show available documents"
    echo "  ./add-knowledge-base.sh /path/to/your/docs     # Ingest your documents"
    echo "  ./add-knowledge-base.sh                        # Use sample documents"
    exit 1
fi

echo "📚 Ingesting knowledge base from: $INGEST_PATH"
echo "⏳ Processing documents..."

# Call the ingest API
RESPONSE=$(curl -s -X POST \
  -H "Content-Type: application/json" \
  -d "{\"directoryOrZipPath\":\"$INGEST_PATH\"}" \
  http://localhost:5000/api/ingest)

# Check if curl was successful
if [ $? -ne 0 ]; then
    echo "❌ Failed to call ingest API"
    exit 1
fi

# Parse response
if echo "$RESPONSE" | jq -e '.success' > /dev/null 2>&1; then
    FILES_PROCESSED=$(echo "$RESPONSE" | jq -r '.processedFiles // 0')
    CHUNKS_GENERATED=$(echo "$RESPONSE" | jq -r '.generatedChunks // 0')
    PROCESSING_TIME=$(echo "$RESPONSE" | jq -r '.metadata.processingTimeMs // 0')
    
    echo "✅ Knowledge base ingested successfully!"
    echo ""
    echo "📊 Results:"
    echo "  • Files processed: $FILES_PROCESSED"
    echo "  • Chunks generated: $CHUNKS_GENERATED" 
    echo "  • Processing time: ${PROCESSING_TIME}ms"
    echo ""
    echo "🎉 Ready to ask questions!"
    echo ""
    echo "💡 Try asking:"
    echo "  • 'What are the safety procedures?'"
    echo "  • 'How do I report an incident?'"
    echo "  • 'What PPE is required?'"
    echo ""
    echo "🌐 Use the frontend: http://localhost:8080"
    
else
    echo "❌ Ingestion failed!"
    echo "Response: $RESPONSE"
    echo ""
    echo "🔍 Check API logs: tail -f logs/api.log"
fi
