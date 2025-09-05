#!/bin/bash

# Test script for OHS Copilot Demo Mode

echo "Testing OHS Copilot API..."

# Test health endpoint
echo "1. Testing health endpoint..."
curl -s http://localhost:5000/api/health | jq .

# Test ask endpoint with demo mode
echo -e "\n2. Testing ask endpoint..."
curl -s -X POST http://localhost:5000/api/ask \
  -H "Content-Type: application/json" \
  -d '{"question": "What are my safety benefits?", "maxTokens": 2000}' | jq .

# Test draft letter endpoint
echo -e "\n3. Testing draft letter endpoint..."
curl -s -X POST http://localhost:5000/api/draft-letter \
  -H "Content-Type: application/json" \
  -d '{"purpose": "incident notification", "points": ["Investigation scheduled", "Documentation required"]}' | jq .

# Test metrics endpoint
echo -e "\n4. Testing metrics endpoint..."
curl -s http://localhost:5000/api/metrics | jq .

echo -e "\nAll tests completed!"
