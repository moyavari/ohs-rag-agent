#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- Create pgvector extension
    CREATE EXTENSION IF NOT EXISTS vector;
    
    -- Create memory database
    CREATE DATABASE ohscopilot_memory;
    
    -- Connect to memory database and create extension
    \\c ohscopilot_memory;
    CREATE EXTENSION IF NOT EXISTS vector;
EOSQL
