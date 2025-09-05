#!/bin/bash

# Start OHS Copilot Frontend
echo "🌐 Starting OHS Copilot Frontend"
echo "================================"

# Check if API is running
if curl -s http://localhost:5000/api/health > /dev/null 2>&1; then
    echo "✅ API is running at http://localhost:5000"
else
    echo "⚠️  API is not running. Starting in demo mode..."
    echo ""
    
    # Start API in background
    cd "$(dirname "$0")"
    export DEMO_MODE=true
    export VECTOR_STORE=json
    export AOAI_ENDPOINT=https://demo.openai.azure.com/
    export AOAI_API_KEY=demo-key
    export AOAI_CHAT_DEPLOYMENT=gpt-4
    export AOAI_EMB_DEPLOYMENT=text-embedding-ada-002
    export MEMORY_BACKEND=memory
    
    echo "Starting OHS Copilot API in demo mode..."
    cd src/OHS.Copilot.API
    dotnet run --urls "http://localhost:5000" > ../../../logs/api.log 2>&1 &
    API_PID=$!
    cd ../..
    
    echo "Waiting for API to start..."
    for i in {1..30}; do
        if curl -s http://localhost:5000/api/health > /dev/null 2>&1; then
            echo "✅ API started successfully"
            break
        fi
        sleep 1
        if [ $i -eq 30 ]; then
            echo "❌ API failed to start. Check logs/api.log"
            exit 1
        fi
    done
fi

# Detect available browser
BROWSER=""
if command -v google-chrome > /dev/null 2>&1; then
    BROWSER="google-chrome"
elif command -v chromium-browser > /dev/null 2>&1; then
    BROWSER="chromium-browser"
elif command -v firefox > /dev/null 2>&1; then
    BROWSER="firefox"
elif command -v microsoft-edge > /dev/null 2>&1; then
    BROWSER="microsoft-edge"
fi

# Start a simple HTTP server for the frontend
echo ""
echo "🚀 Starting frontend web server..."

# Try to use Python's built-in HTTP server
if command -v python3 > /dev/null 2>&1; then
    echo "Using Python 3 HTTP server"
    cd frontend
    echo "Frontend available at: http://localhost:8080"
    echo "Press Ctrl+C to stop"
    
    # Open browser if available
    if [ -n "$BROWSER" ]; then
        echo "Opening in $BROWSER..."
        $BROWSER "http://localhost:8080" > /dev/null 2>&1 &
    fi
    
    python3 -m http.server 8080
    
elif command -v python > /dev/null 2>&1; then
    echo "Using Python 2 HTTP server"
    cd frontend
    echo "Frontend available at: http://localhost:8080"
    echo "Press Ctrl+C to stop"
    
    # Open browser if available
    if [ -n "$BROWSER" ]; then
        echo "Opening in $BROWSER..."
        $BROWSER "http://localhost:8080" > /dev/null 2>&1 &
    fi
    
    python -m SimpleHTTPServer 8080
    
elif command -v node > /dev/null 2>&1; then
    echo "Using Node.js HTTP server"
    cd frontend
    echo "Frontend available at: http://localhost:8080"
    echo "Press Ctrl+C to stop"
    
    # Open browser if available
    if [ -n "$BROWSER" ]; then
        echo "Opening in $BROWSER..."
        $BROWSER "http://localhost:8080" > /dev/null 2>&1 &
    fi
    
    npx http-server -p 8080
    
else
    # Fallback - just open the file directly
    echo "No HTTP server available. Opening file directly..."
    FRONTEND_FILE="$(pwd)/frontend/index.html"
    
    if [ -n "$BROWSER" ]; then
        echo "Opening in $BROWSER..."
        $BROWSER "file://$FRONTEND_FILE"
    else
        echo "📂 Please open this file in your browser:"
        echo "file://$FRONTEND_FILE"
    fi
fi

echo ""
echo "🎉 OHS Copilot Frontend is ready!"
echo "================================"
echo ""
echo "💡 Usage Tips:"
echo "• Ask questions in natural language"
echo "• Use conversation IDs for multi-turn dialogues"
echo "• Draft letters with key points"
echo "• Monitor system metrics and performance"
echo ""
echo "🔧 Configuration:"
echo "• API URL: http://localhost:5000"
echo "• Demo Mode: Enabled (no Azure OpenAI required)"
echo "• Vector Store: JSON (in-memory)"
echo ""
echo "📖 For more information, see docs/DEVELOPMENT.md"
