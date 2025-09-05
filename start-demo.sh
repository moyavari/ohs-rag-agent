#!/bin/bash

# Start the application in demo mode
cd /home/mohammad/ohs/src/OHS.Copilot.API

export DEMO_MODE=true
export VECTOR_STORE=json
export AOAI_ENDPOINT=https://demo.openai.azure.com/
export AOAI_API_KEY=demo-key
export AOAI_CHAT_DEPLOYMENT=gpt-4
export AOAI_EMB_DEPLOYMENT=text-embedding-ada-002
export MEMORY_BACKEND=cosmos

echo "Starting OHS Copilot in Demo Mode..."
echo "API will be available at http://localhost:5000"
echo "Press Ctrl+C to stop"

# Run the application in foreground
dotnet run --urls "http://localhost:5000"
