using System.Text.Json;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.DTOs.Requests;
using OHS.Copilot.Application.DTOs.Responses;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Configuration;
using OHS.Copilot.Domain.ValueObjects;

namespace OHS.Copilot.Infrastructure.Services;

public class DemoModeService : IDemoModeService
{
    private readonly AppSettings _settings;
    private readonly ILogger<DemoModeService> _logger;
    private readonly Dictionary<string, DemoFixture> _askFixtures = [];
    private readonly Dictionary<string, DemoFixture> _letterFixtures = [];
    private readonly Dictionary<string, DemoTrace> _traces = [];

    public DemoModeService(AppSettings settings, ILogger<DemoModeService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsDemoModeEnabled()
    {
        return _settings.DemoMode;
    }

    public async Task<AskResponse?> GetDemoAskResponseAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsDemoModeEnabled())
        {
            return null;
        }

        await EnsureFixturesLoadedAsync(cancellationToken);

        var fixtureKey = GenerateFixtureKey(request.Question);
        
        if (_askFixtures.TryGetValue(fixtureKey, out var fixture) && fixture.Response is JsonElement jsonResponse)
        {
            try
            {
                var response = JsonSerializer.Deserialize<AskResponse>(jsonResponse.GetRawText(), JsonOptions);
                
                if (response != null)
                {
                    response.ConversationId = request.ConversationId;
                    
                    _logger.LogDebug("Returning demo fixture response for question: {Question}", 
                        request.Question.Substring(0, Math.Min(50, request.Question.Length)));
                    
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize demo fixture for key: {Key}", fixtureKey);
            }
        }

        return CreateFallbackAskResponse(request);
    }

    public async Task<DraftLetterResponse?> GetDemoLetterResponseAsync(DraftLetterRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsDemoModeEnabled())
        {
            return null;
        }

        await EnsureFixturesLoadedAsync(cancellationToken);

        var fixtureKey = GenerateFixtureKey(request.Purpose);
        
        if (_letterFixtures.TryGetValue(fixtureKey, out var fixture) && fixture.Response is JsonElement jsonResponse)
        {
            try
            {
                var response = JsonSerializer.Deserialize<DraftLetterResponse>(jsonResponse.GetRawText(), JsonOptions);
                
                if (response != null)
                {
                    response.ConversationId = request.ConversationId;
                    
                    _logger.LogDebug("Returning demo fixture letter for purpose: {Purpose}", 
                        request.Purpose.Substring(0, Math.Min(50, request.Purpose.Length)));
                    
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize demo letter fixture for key: {Key}", fixtureKey);
            }
        }

        return CreateFallbackLetterResponse(request);
    }

    public async Task<List<DemoFixture>> LoadFixturesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureFixturesLoadedAsync(cancellationToken);
        
        var allFixtures = new List<DemoFixture>();
        allFixtures.AddRange(_askFixtures.Values);
        allFixtures.AddRange(_letterFixtures.Values);
        
        return allFixtures;
    }

    public async Task<DemoTrace?> GetDemoTraceAsync(string traceId, CancellationToken cancellationToken = default)
    {
        await EnsureTracesLoadedAsync(cancellationToken);
        
        _traces.TryGetValue(traceId, out var trace);
        return trace;
    }

    private async Task EnsureFixturesLoadedAsync(CancellationToken cancellationToken)
    {
        if (_askFixtures.Count > 0) return;

        try
        {
            await LoadAskFixturesAsync(cancellationToken);
            await LoadLetterFixturesAsync(cancellationToken);
            
            _logger.LogInformation("Loaded demo fixtures: {AskCount} ask, {LetterCount} letter",
                _askFixtures.Count, _letterFixtures.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load demo fixtures, using fallback responses");
            await CreateDefaultFixturesAsync(cancellationToken);
        }
    }

    private async Task LoadAskFixturesAsync(CancellationToken cancellationToken)
    {
        var fixturesPath = Path.Combine(_settings.Demo.FixturesPath, "ask-fixtures.json");
        
        if (!File.Exists(fixturesPath))
        {
            await CreateDefaultAskFixturesAsync(fixturesPath, cancellationToken);
        }

        var json = await File.ReadAllTextAsync(fixturesPath, cancellationToken);
        var fixtures = JsonSerializer.Deserialize<List<DemoFixture>>(json, JsonOptions);
        
        if (fixtures != null)
        {
            foreach (var fixture in fixtures)
            {
                var key = GenerateFixtureKey(GetQuestionFromRequest(fixture.Request));
                _askFixtures[key] = fixture;
            }
        }
    }

    private async Task LoadLetterFixturesAsync(CancellationToken cancellationToken)
    {
        var fixturesPath = Path.Combine(_settings.Demo.FixturesPath, "letter-fixtures.json");
        
        if (!File.Exists(fixturesPath))
        {
            await CreateDefaultLetterFixturesAsync(fixturesPath, cancellationToken);
        }

        var json = await File.ReadAllTextAsync(fixturesPath, cancellationToken);
        var fixtures = JsonSerializer.Deserialize<List<DemoFixture>>(json, JsonOptions);
        
        if (fixtures != null)
        {
            foreach (var fixture in fixtures)
            {
                var key = GenerateFixtureKey(GetPurposeFromRequest(fixture.Request));
                _letterFixtures[key] = fixture;
            }
        }
    }

    private async Task EnsureTracesLoadedAsync(CancellationToken cancellationToken)
    {
        if (_traces.Count > 0) return;

        try
        {
            var tracesPath = Path.Combine(_settings.Demo.TracePath, "demo-traces.json");
            
            if (!File.Exists(tracesPath))
            {
                await CreateDefaultTracesAsync(tracesPath, cancellationToken);
            }

            var json = await File.ReadAllTextAsync(tracesPath, cancellationToken);
            var traces = JsonSerializer.Deserialize<List<DemoTrace>>(json, JsonOptions);
            
            if (traces != null)
            {
                foreach (var trace in traces)
                {
                    _traces[trace.TraceId] = trace;
                }
            }

            _logger.LogInformation("Loaded {Count} demo traces", _traces.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load demo traces");
        }
    }

    private async Task CreateDefaultFixturesAsync(CancellationToken cancellationToken)
    {
        await CreateDefaultAskFixturesAsync(Path.Combine(_settings.Demo.FixturesPath, "ask-fixtures.json"), cancellationToken);
        await CreateDefaultLetterFixturesAsync(Path.Combine(_settings.Demo.FixturesPath, "letter-fixtures.json"), cancellationToken);
    }

    private async Task CreateDefaultAskFixturesAsync(string filePath, CancellationToken cancellationToken)
    {
        var fixtures = new List<DemoFixture>();

        var defaultQuestions = new[]
        {
            ("ppe requirements", "What PPE is required for construction work?"),
            ("incident reporting", "How do I report a workplace incident?"),
            ("emergency procedures", "What should I do in a fire emergency?"),
            ("chemical safety", "How should chemicals be stored and handled?"),
            ("return to work", "What is required for returning to work after an injury?")
        };

        foreach (var (key, question) in defaultQuestions)
        {
            var request = new AskRequest { Question = question, MaxTokens = 500 };
            var response = CreateDemoAskResponse(question, key);
            var fixture = DemoFixture.CreateAskFixture(key, request, response);
            
            fixtures.Add(fixture);
            _askFixtures[GenerateFixtureKey(question)] = fixture;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _settings.Demo.FixturesPath);
        var json = JsonSerializer.Serialize(fixtures, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Created {Count} default ask fixtures", fixtures.Count);
    }

    private async Task CreateDefaultLetterFixturesAsync(string filePath, CancellationToken cancellationToken)
    {
        var fixtures = new List<DemoFixture>();

        var defaultLetters = new[]
        {
            ("incident notification", "Notify employee about incident investigation", new[] { "Investigation scheduled", "Documentation required", "Cooperation expected" }),
            ("return to work", "Return to work authorization", new[] { "Medical clearance received", "Modified duties approved", "Follow-up scheduled" }),
            ("safety reminder", "Remind about PPE compliance", new[] { "Recent violations noted", "Training required", "Policy review needed" })
        };

        foreach (var (key, purpose, points) in defaultLetters)
        {
            var request = new DraftLetterRequest { Purpose = purpose, Points = points.ToList() };
            var response = CreateDemoLetterResponse(purpose, points.ToList());
            var fixture = DemoFixture.CreateLetterFixture(key, request, response);
            
            fixtures.Add(fixture);
            _letterFixtures[GenerateFixtureKey(purpose)] = fixture;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _settings.Demo.FixturesPath);
        var json = JsonSerializer.Serialize(fixtures, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Created {Count} default letter fixtures", fixtures.Count);
    }

    private async Task CreateDefaultTracesAsync(string filePath, CancellationToken cancellationToken)
    {
        var traces = new List<DemoTrace>
        {
            DemoTrace.Create("demo-ask-trace-001", "ask", new List<DemoSpan>
            {
                new() { SpanId = "router-001", Name = "Router", Kind = "Agent", Duration = TimeSpan.FromMilliseconds(2) },
                new() { SpanId = "retriever-001", Name = "Retriever", Kind = "Agent", Duration = TimeSpan.FromMilliseconds(15) },
                new() { SpanId = "drafter-001", Name = "Drafter", Kind = "Agent", Duration = TimeSpan.FromMilliseconds(120) },
                new() { SpanId = "citechecker-001", Name = "CiteChecker", Kind = "Agent", Duration = TimeSpan.FromMilliseconds(8) }
            }),
            
            DemoTrace.Create("demo-letter-trace-001", "draft", new List<DemoSpan>
            {
                new() { SpanId = "router-002", Name = "Router", Kind = "Agent", Duration = TimeSpan.FromMilliseconds(1) },
                new() { SpanId = "retriever-002", Name = "Retriever", Kind = "Agent", Duration = TimeSpan.FromMilliseconds(12) },
                new() { SpanId = "drafter-002", Name = "Drafter", Kind = "Agent", Duration = TimeSpan.FromMilliseconds(95) },
                new() { SpanId = "citechecker-002", Name = "CiteChecker", Kind = "Agent", Duration = TimeSpan.FromMilliseconds(5) }
            })
        };

        foreach (var trace in traces)
        {
            _traces[trace.TraceId] = trace;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _settings.Demo.TracePath);
        var json = JsonSerializer.Serialize(traces, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Created {Count} default demo traces", traces.Count);
    }

    private AskResponse CreateDemoAskResponse(string question, string category)
    {
        var answer = category switch
        {
            "ppe requirements" => "All construction workers must wear hard hats, safety glasses, and steel-toed boots [#1]. PPE must meet ANSI Z89.1 standards [#2].",
            "incident reporting" => "Report all workplace incidents within 24 hours using Form WS-101 [#1]. Include date, time, location, personnel involved, and witness statements [#2].",
            "emergency procedures" => "In case of fire: activate alarm, evacuate via nearest exit, proceed to muster point in parking lot [#1]. Do not use elevators during emergencies [#2].",
            "chemical safety" => "Store chemicals in appropriate labeled containers [#1]. Only trained personnel may handle hazardous materials with proper PPE [#2].",
            "return to work" => "Medical clearance from healthcare provider is required [#1]. Gradual return-to-work program available with modified duties [#2].",
            _ => "Based on workplace safety policies, please refer to your safety coordinator for specific guidance [#1]."
        };

        var citations = new List<CitationDto>
        {
            new() { Id = "c1", Title = "Safety Policy Manual", Text = "Relevant policy excerpt...", Score = 0.95 },
            new() { Id = "c2", Title = "Workplace Guidelines", Text = "Additional guidance...", Score = 0.88 }
        };

        return new AskResponse
        {
            Answer = answer,
            Citations = citations,
            Metadata = new ResponseMetadata
            {
                PromptSha = "DEMO_" + ComputeQuestionHash(question),
                Model = "demo-model",
                ProcessingTime = TimeSpan.FromMilliseconds(145),
                AgentTraces = CreateDemoAgentTraces()
            }
        };
    }

    private DraftLetterResponse CreateDemoLetterResponse(string purpose, List<string> points)
    {
        var subject = purpose.Contains("incident") ? "Workplace Incident Investigation Notice" :
                     purpose.Contains("return") ? "Return to Work Authorization" :
                     "Workplace Safety Communication";

        var pointsText = string.Join("\n", points.Select((p, i) => $"{i + 1}. {p}"));
        
        var body = "Dear {{recipient_name}},\n\n" +
                   $"I am writing to inform you regarding {{{{case_reference}}}} in relation to {purpose.ToLower()}.\n\n" +
                   "Based on our review, the following items require attention:\n\n" +
                   pointsText + "\n\n" +
                   "Please contact the safety coordinator at {{coordinator_phone}} if you have any questions about these requirements.\n\n" +
                   "The next steps will be communicated to you within {{timeline}} business days.\n\n" +
                   "Sincerely,\n\n" +
                   "{{sender_name}}\n" +
                   "{{sender_title}}\n" +
                   "Workplace Safety Department";

        var placeholders = new List<string>
        {
            "recipient_name", "case_reference", "coordinator_phone", "timeline", "sender_name", "sender_title"
        };

        return new DraftLetterResponse
        {
            Subject = subject,
            Body = body,
            Placeholders = placeholders,
            Metadata = new ResponseMetadata
            {
                PromptSha = "DEMO_" + ComputeQuestionHash(purpose),
                Model = "demo-model", 
                ProcessingTime = TimeSpan.FromMilliseconds(112),
                AgentTraces = CreateDemoAgentTraces()
            }
        };
    }

    private AskResponse CreateFallbackAskResponse(AskRequest request)
    {
        var answer = "This is a demo response for your question about workplace safety. " +
                    "In demo mode, responses are generated from pre-recorded fixtures. " +
                    "For specific safety guidance, please consult your safety coordinator [#1].";

        var citations = new List<CitationDto>
        {
            new() { Id = "c1", Title = "Demo Safety Manual", Text = "This is demo content for testing purposes", Score = 1.0 }
        };

        return new AskResponse
        {
            Answer = answer,
            Citations = citations,
            ConversationId = request.ConversationId,
            Metadata = new ResponseMetadata
            {
                PromptSha = "DEMO_FALLBACK_" + ComputeQuestionHash(request.Question),
                Model = "demo-model",
                ProcessingTime = TimeSpan.FromMilliseconds(100),
                AgentTraces = CreateDemoAgentTraces()
            }
        };
    }

    private DraftLetterResponse CreateFallbackLetterResponse(DraftLetterRequest request)
    {
        var subject = "Workplace Safety Communication - Demo Mode";
        var pointsText = string.Join("\n", request.Points.Select((p, i) => $"{i + 1}. {p}"));
        
        var body = "Dear {{recipient_name}},\n\n" +
                   $"This is a demo letter generated in response to: {request.Purpose}\n\n" +
                   "Key points addressed:\n" +
                   pointsText + "\n\n" +
                   "This letter was generated in demo mode for testing purposes.\n\n" +
                   "Best regards,\n\n" +
                   "{{sender_name}}\n" +
                   "Demo Safety Department";

        var placeholders = new List<string> { "recipient_name", "sender_name" };

        return new DraftLetterResponse
        {
            Subject = subject,
            Body = body,
            Placeholders = placeholders,
            ConversationId = request.ConversationId,
            Metadata = new ResponseMetadata
            {
                PromptSha = "DEMO_FALLBACK_" + ComputeQuestionHash(request.Purpose),
                Model = "demo-model",
                ProcessingTime = TimeSpan.FromMilliseconds(85),
                AgentTraces = CreateDemoAgentTraces()
            }
        };
    }

    private Dictionary<string, object> CreateDemoAgentTraces()
    {
        return new Dictionary<string, object>
        {
            ["total_agents"] = 4,
            ["total_duration_ms"] = 145.0,
            ["traces"] = new List<object>
            {
                new { agent = "Router", action = "execute", duration_ms = 2.0, timestamp = DateTime.UtcNow },
                new { agent = "Retriever", action = "execute", duration_ms = 15.0, timestamp = DateTime.UtcNow },
                new { agent = "Drafter", action = "execute", duration_ms = 120.0, timestamp = DateTime.UtcNow },
                new { agent = "CiteChecker", action = "execute", duration_ms = 8.0, timestamp = DateTime.UtcNow }
            }
        };
    }

    private string GenerateFixtureKey(string input)
    {
        return input.ToLower()
            .Replace(" ", "")
            .Replace("?", "")
            .Replace("!", "")
            .Replace(".", "")
            .Replace(",", "")
            .Substring(0, Math.Min(20, input.Length));
    }

    private string GetQuestionFromRequest(object request)
    {
        if (request is JsonElement jsonElement && jsonElement.TryGetProperty("question", out var questionProp))
        {
            return questionProp.GetString() ?? "";
        }
        return "";
    }

    private string GetPurposeFromRequest(object request)
    {
        if (request is JsonElement jsonElement && jsonElement.TryGetProperty("purpose", out var purposeProp))
        {
            return purposeProp.GetString() ?? "";
        }
        return "";
    }

    private string ComputeQuestionHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).Substring(0, 8);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
