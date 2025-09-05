#!/bin/bash

# Stop All OHS Copilot Services
echo "🛑 Stopping OHS Copilot Services"
echo "================================="

# Function to kill processes by pattern
kill_by_pattern() {
    local pattern="$1"
    local description="$2"
    
    local pids=$(pgrep -f "$pattern" 2>/dev/null)
    if [ -n "$pids" ]; then
        echo "🔄 Stopping $description..."
        echo "$pids" | xargs kill -TERM 2>/dev/null
        sleep 2
        
        # Force kill if still running
        local still_running=$(pgrep -f "$pattern" 2>/dev/null)
        if [ -n "$still_running" ]; then
            echo "💥 Force stopping $description..."
            echo "$still_running" | xargs kill -KILL 2>/dev/null
        fi
        echo "✅ $description stopped"
    else
        echo "ℹ️  No $description processes found"
    fi
}

# Stop API processes
kill_by_pattern "dotnet.*OHS.Copilot.API" "OHS Copilot API"
kill_by_pattern "dotnet run.*5000" "Dotnet API on port 5000"

# Stop frontend processes  
kill_by_pattern "python.*http.server.*8080" "Frontend HTTP Server"
kill_by_pattern "python3.*http.server.*8080" "Python3 HTTP Server"

# Stop any background demo processes
kill_by_pattern "start-frontend.sh" "Frontend startup script"
kill_by_pattern "start-demo" "Demo startup scripts"

# Check and kill processes by port
echo ""
echo "🔍 Checking ports..."

# Kill anything on port 5000 (API)
API_PID=$(lsof -ti:5000 2>/dev/null)
if [ -n "$API_PID" ]; then
    echo "🔄 Stopping process on port 5000 (API)..."
    kill -TERM $API_PID 2>/dev/null
    sleep 2
    if kill -0 $API_PID 2>/dev/null; then
        kill -KILL $API_PID 2>/dev/null
    fi
    echo "✅ Port 5000 cleared"
else
    echo "ℹ️  Port 5000 is free"
fi

# Kill anything on port 8080 (Frontend)  
FRONTEND_PID=$(lsof -ti:8080 2>/dev/null)
if [ -n "$FRONTEND_PID" ]; then
    echo "🔄 Stopping process on port 8080 (Frontend)..."
    kill -TERM $FRONTEND_PID 2>/dev/null
    sleep 2
    if kill -0 $FRONTEND_PID 2>/dev/null; then
        kill -KILL $FRONTEND_PID 2>/dev/null
    fi
    echo "✅ Port 8080 cleared"
else
    echo "ℹ️  Port 8080 is free"
fi

# Stop Docker containers if running
echo ""
echo "🐳 Checking Docker containers..."
if command -v docker >/dev/null 2>&1; then
    CONTAINERS=$(docker ps --filter "name=ohs-copilot" --format "{{.Names}}" 2>/dev/null)
    if [ -n "$CONTAINERS" ]; then
        echo "🔄 Stopping OHS Copilot Docker containers..."
        echo "$CONTAINERS" | xargs docker stop 2>/dev/null
        echo "✅ Docker containers stopped"
    else
        echo "ℹ️  No OHS Copilot Docker containers running"
    fi
fi

# Clean up any zombie processes
echo ""
echo "🧹 Cleaning up..."

# Remove any leftover lock files
rm -f logs/api.pid logs/frontend.pid 2>/dev/null

# Check final status
echo ""
echo "📊 Final Status:"
API_RUNNING=$(curl -s http://localhost:5000/api/health >/dev/null 2>&1 && echo "RUNNING" || echo "STOPPED")
FRONTEND_RUNNING=$(curl -s -I http://localhost:8080 >/dev/null 2>&1 && echo "RUNNING" || echo "STOPPED")

echo "• API (port 5000): $API_RUNNING"
echo "• Frontend (port 8080): $FRONTEND_RUNNING"

if [ "$API_RUNNING" = "STOPPED" ] && [ "$FRONTEND_RUNNING" = "STOPPED" ]; then
    echo ""
    echo "✅ All OHS Copilot services stopped successfully!"
    echo ""
    echo "💡 To restart:"
    echo "• Demo mode: ./start-demo.sh"
    echo "• With OpenAI: ./start-demo-openai.sh"
    echo "• Frontend only: ./start-frontend.sh"
else
    echo ""
    echo "⚠️  Some services may still be running"
    echo "💡 You can also manually close any terminal windows running the services"
fi

echo ""
echo "🎉 Cleanup complete!"
