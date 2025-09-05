-- Initialize PostgreSQL database for OHS Copilot
-- This script creates the required extensions and tables

-- Enable pgvector extension for vector operations
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create schema for application
CREATE SCHEMA IF NOT EXISTS ohs_copilot;
SET search_path TO ohs_copilot;

-- Chunks table for document storage
CREATE TABLE IF NOT EXISTS chunks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    text TEXT NOT NULL,
    title VARCHAR(500) NOT NULL,
    section VARCHAR(500),
    chunk_index INTEGER NOT NULL DEFAULT 0,
    total_chunks INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Embeddings table for vector storage
CREATE TABLE IF NOT EXISTS embeddings (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    chunk_id UUID NOT NULL REFERENCES chunks(id) ON DELETE CASCADE,
    vector VECTOR(1536), -- Azure OpenAI ada-002 dimensions
    model VARCHAR(100) NOT NULL DEFAULT 'text-embedding-ada-002',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(chunk_id, model)
);

-- Create vector similarity search index
CREATE INDEX IF NOT EXISTS embeddings_vector_idx ON embeddings USING ivfflat (vector vector_cosine_ops) WITH (lists = 100);

-- Audit logs table
CREATE TABLE IF NOT EXISTS audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    operation VARCHAR(100) NOT NULL,
    user_id VARCHAR(100) NOT NULL DEFAULT 'anonymous',
    correlation_id UUID NOT NULL,
    input_data JSONB,
    output_data JSONB,
    metadata JSONB,
    processing_time_ms INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Conversation memory table
CREATE TABLE IF NOT EXISTS conversation_memory (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    conversation_id VARCHAR(100) NOT NULL,
    turn_index INTEGER NOT NULL,
    user_message TEXT NOT NULL,
    assistant_message TEXT NOT NULL,
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(conversation_id, turn_index)
);

-- Persona memory table
CREATE TABLE IF NOT EXISTS persona_memory (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id VARCHAR(100) NOT NULL PRIMARY KEY,
    persona_type VARCHAR(50) NOT NULL,
    custom_profile JSONB,
    preferences TEXT[],
    description TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Policy memory table
CREATE TABLE IF NOT EXISTS policy_memory (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    title VARCHAR(500) NOT NULL,
    content TEXT NOT NULL,
    category VARCHAR(100) NOT NULL,
    effective_date DATE,
    expiry_date DATE,
    version INTEGER NOT NULL DEFAULT 1,
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Prompt versions table
CREATE TABLE IF NOT EXISTS prompt_versions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    prompt_name VARCHAR(200) NOT NULL,
    version_hash VARCHAR(64) NOT NULL,
    prompt_template TEXT NOT NULL,
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(prompt_name, version_hash)
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_chunks_title ON chunks(title);
CREATE INDEX IF NOT EXISTS idx_audit_logs_operation ON audit_logs(operation);
CREATE INDEX IF NOT EXISTS idx_audit_logs_correlation ON audit_logs(correlation_id);
CREATE INDEX IF NOT EXISTS idx_conversation_memory_id ON conversation_memory(conversation_id);
CREATE INDEX IF NOT EXISTS idx_persona_memory_user ON persona_memory(user_id);
CREATE INDEX IF NOT EXISTS idx_policy_memory_category ON policy_memory(category);
CREATE INDEX IF NOT EXISTS idx_prompt_versions_name ON prompt_versions(prompt_name);

-- Grant permissions
GRANT ALL PRIVILEGES ON SCHEMA ohs_copilot TO ohsuser;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA ohs_copilot TO ohsuser;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA ohs_copilot TO ohsuser;
