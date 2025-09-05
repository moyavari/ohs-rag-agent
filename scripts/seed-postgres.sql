-- Seed data for OHS Copilot PostgreSQL database
SET search_path TO ohs_copilot;

-- Insert sample policy documents
INSERT INTO policy_memory (title, content, category, effective_date) VALUES 
('Emergency Evacuation Procedures', 
 'All employees must familiarize themselves with evacuation routes. In case of emergency, proceed to the nearest exit calmly. Do not use elevators. Report to designated assembly points.',
 'Emergency Response', 
 CURRENT_DATE),

('Personal Protective Equipment Policy',
 'All workers in designated areas must wear appropriate PPE including hard hats, safety glasses, and steel-toed boots. PPE must be inspected before each use.',
 'Safety Equipment',
 CURRENT_DATE),

('Chemical Safety Guidelines',
 'Handle all chemicals according to their Safety Data Sheets (SDS). Ensure proper ventilation when working with volatile substances. Report all spills immediately.',
 'Chemical Safety',
 CURRENT_DATE),

('Incident Reporting Procedures',
 'All workplace incidents must be reported within 24 hours using Form HS-101. Include witness statements and photographic evidence when possible.',
 'Reporting',
 CURRENT_DATE),

('Lockout/Tagout Procedures',
 'Before performing maintenance on equipment, ensure proper lockout/tagout procedures are followed. Only authorized personnel may remove locks or tags.',
 'Maintenance Safety',
 CURRENT_DATE);

-- Insert sample conversation memory
INSERT INTO conversation_memory (conversation_id, turn_index, user_message, assistant_message, metadata) VALUES
('demo-conv-001', 1, 
 'What are the evacuation procedures?',
 'According to our Emergency Evacuation Procedures, all employees must proceed to the nearest exit calmly and report to designated assembly points. Do not use elevators during emergencies.',
 '{"processing_time_ms": 150, "citations": ["Emergency Evacuation Procedures"]}'),

('demo-conv-001', 2,
 'What PPE is required?',
 'Based on our Personal Protective Equipment Policy, workers in designated areas must wear hard hats, safety glasses, and steel-toed boots. All PPE must be inspected before use.',
 '{"processing_time_ms": 120, "citations": ["Personal Protective Equipment Policy"]}');

-- Insert sample persona profiles
INSERT INTO persona_memory (user_id, persona_type, custom_profile, preferences, description) VALUES
('safety-inspector-001', 'Inspector', 
 '{"department": "Safety & Compliance", "certifications": ["OSHA 30", "HAZMAT"], "experience_years": 8}',
 ARRAY['detailed_responses', 'regulatory_focus', 'incident_analysis'],
 'Senior Safety Inspector with focus on regulatory compliance and incident investigation'),

('safety-coordinator-002', 'Coordinator',
 '{"department": "Operations", "responsibilities": ["training", "audits"], "locations": ["Building A", "Building B"]}', 
 ARRAY['concise_summaries', 'training_materials', 'policy_updates'],
 'Safety Coordinator responsible for training programs and policy implementation'),

('maintenance-supervisor-003', 'Supervisor',
 '{"department": "Maintenance", "specialties": ["electrical", "mechanical"], "team_size": 12}',
 ARRAY['technical_details', 'equipment_focus', 'preventive_maintenance'],
 'Maintenance Supervisor specializing in electrical and mechanical systems');

-- Insert sample audit log entries
INSERT INTO audit_logs (operation, user_id, correlation_id, input_data, output_data, processing_time_ms) VALUES
(uuid_generate_v4(), 'ask', 'safety-inspector-001', uuid_generate_v4(),
 '{"question": "What are chemical safety requirements?"}',
 '{"answer": "Chemical safety requires proper SDS handling...", "citations": 2}',
 180),

(uuid_generate_v4(), 'draft', 'safety-coordinator-002', uuid_generate_v4(),
 '{"purpose": "safety reminder", "points": ["PPE compliance", "Training completion"]}',
 '{"subject": "Safety Reminder - PPE and Training", "body_length": 350}',
 220),

(uuid_generate_v4(), 'ingest', 'system', uuid_generate_v4(),
 '{"directory": "/data/policies", "file_count": 5}',
 '{"chunks_processed": 45, "files_processed": 5, "success": true}',
 2500);

-- Insert sample prompt versions
INSERT INTO prompt_versions (prompt_name, version_hash, prompt_template, metadata) VALUES
('ask_prompt_v1', 'abc123def456', 
 'You are a safety expert assistant. Answer the following question based on the provided context: {question}\n\nContext: {context}\n\nProvide a clear, actionable response.',
 '{"model": "gpt-4", "temperature": 0.1, "max_tokens": 1000}'),

('draft_letter_v1', 'def456ghi789',
 'Draft a professional letter for: {purpose}\n\nKey points to include:\n{points}\n\nEnsure the tone is professional and includes all necessary safety considerations.',
 '{"model": "gpt-4", "temperature": 0.2, "max_tokens": 800}'),

('router_prompt_v1', 'ghi789jkl012',
 'Analyze this request and determine the appropriate action: {request}\n\nClassify as: ask, draft, or ingest',
 '{"model": "gpt-4", "temperature": 0.0, "max_tokens": 100}');

COMMIT;
