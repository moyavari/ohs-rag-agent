using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.ValueObjects;
using OHS.Copilot.Infrastructure.Services;

namespace OHS.Copilot.Infrastructure.Agents;

public class DrafterAgent : BaseAgent
{
    private readonly SemanticKernelService _kernelService;
    private readonly IMemoryService _memoryService;

    public override string Name => "Drafter";

    public DrafterAgent(Kernel kernel, ILogger<DrafterAgent> logger, SemanticKernelService kernelService, IMemoryService memoryService, ITelemetryService? telemetryService = null) 
        : base(kernel, logger, telemetryService)
    {
        _kernelService = kernelService;
        _memoryService = memoryService;
    }

    protected override async Task<AgentResult> ExecuteInternalAsync(AgentContext agentContext, CancellationToken cancellationToken)
    {
        var requestType = agentContext.GetData<string>("request_type");
        var parameters = agentContext.GetData<Dictionary<string, object>>("parameters");
        var contextChunks = agentContext.GetData<List<string>>("context_chunks");
        var citations = agentContext.GetData<List<Citation>>("citations");

        if (requestType == null || parameters == null || contextChunks == null || citations == null)
        {
            return AgentResult.Failed("Missing required data from previous agents");
        }

        return requestType switch
        {
            "ask" => await GenerateAnswerAsync(agentContext, parameters, contextChunks, citations, cancellationToken),
            "draft" => await GenerateLetterDraftAsync(agentContext, parameters, contextChunks, cancellationToken),
            _ => AgentResult.Failed($"Unsupported request type: {requestType}")
        };
    }

    private async Task<AgentResult> GenerateAnswerAsync(
        AgentContext agentContext,
        Dictionary<string, object> parameters, 
        List<string> contextChunks,
        List<Citation> citations,
        CancellationToken cancellationToken)
    {
        var question = GetParameterValue(parameters, "Question") ?? "";
        var context = string.Join("\n\n", contextChunks);
        
        var conversationMemory = agentContext.GetData<ConversationMemory>("conversation_memory");
        var personaMemory = agentContext.GetData<PersonaMemory>("persona_memory");
        
        var prompt = CreateAnswerPrompt(question, context, conversationMemory, personaMemory);
        var promptHash = ComputePromptHash(prompt);

        LogAction("generate_answer", new { 
            question = question.Substring(0, Math.Min(50, question.Length)),
            contextLength = context.Length,
            promptHash,
            citationCount = citations.Count 
        });

        try
        {
            var response = await CallAzureOpenAIAsync(prompt, cancellationToken);
            var formattedAnswer = FormatAnswerWithCitations(response, citations);

            var answer = Answer.Create(formattedAnswer, citations);

            agentContext.SetData("answer", answer);
            agentContext.SetData("prompt_hash", promptHash);

            return AgentResult.Successful(new Dictionary<string, object>
            {
                ["answer"] = answer,
                ["prompt_hash"] = promptHash
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate answer");
            return AgentResult.Failed($"Answer generation failed: {ex.Message}");
        }
    }

    private async Task<AgentResult> GenerateLetterDraftAsync(
        AgentContext agentContext,
        Dictionary<string, object> parameters,
        List<string> contextChunks,
        CancellationToken cancellationToken)
    {
        var purpose = GetParameterValue(parameters, "Purpose") ?? "";
        var points = parameters.TryGetValue("Points", out var pointsObj) && pointsObj is List<string> pointsList ? pointsList : [];
        var caseId = GetParameterValue(parameters, "CaseId");
        var context = string.Join("\n\n", contextChunks);
        
        var prompt = CreateLetterPrompt(purpose, points, context, caseId);
        var promptHash = ComputePromptHash(prompt);

        LogAction("generate_letter", new { 
            purpose = purpose.Substring(0, Math.Min(50, purpose.Length)),
            pointCount = points.Count,
            contextLength = context.Length,
            promptHash 
        });

        try
        {
            var response = await CallAzureOpenAIAsync(prompt, cancellationToken);
            var letterDraft = ParseLetterResponse(response);

            agentContext.SetData("letter_draft", letterDraft);
            agentContext.SetData("prompt_hash", promptHash);

            return AgentResult.Successful(new Dictionary<string, object>
            {
                ["letter_draft"] = letterDraft,
                ["prompt_hash"] = promptHash
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate letter draft");
            return AgentResult.Failed($"Letter generation failed: {ex.Message}");
        }
    }

    private string CreateAnswerPrompt(string question, string context, ConversationMemory? conversationMemory = null, PersonaMemory? personaMemory = null)
    {
        var conversationContext = "";
        if (conversationMemory != null && conversationMemory.Turns.Count > 0)
        {
            conversationContext = $"\nPrevious conversation context:\n{conversationMemory.GetRecentContext(2)}\n";
        }

        var personaContext = "";
        if (personaMemory != null)
        {
            var responseStyle = personaMemory.Profile.TryGetValue("ResponseStyle", out var style) ? style : "professional";
            var role = personaMemory.Profile.TryGetValue("Role", out var roleValue) ? roleValue : "safety professional";
            personaContext = $"\nUser persona: {role} - Preferred style: {responseStyle}\n";
        }

        return $"""
        You are an expert workplace safety assistant for a Canadian occupational health and safety organization.

        Context from safety policies and guidelines:
        {context}
        {conversationContext}
        {personaContext}
        Question: {question}

        Instructions:
        - Provide a concise, accurate answer based ONLY on the provided context
        - Include citations using the format [#1], [#2], etc. referring to source documents
        - If the context doesn't contain relevant information, say "I don't have enough information to answer this question"
        - Use neutral, professional tone appropriate for workplace safety guidance
        - Keep the response under 300 words
        - Every factual claim must have a citation
        {(conversationMemory != null ? "- Consider the previous conversation context when appropriate" : "")}
        {(personaMemory != null ? $"- Adapt response style for {personaMemory.Type} persona" : "")}

        Answer:
        """;
    }

    private string CreateLetterPrompt(string purpose, List<string> points, string context, string? caseId)
    {
        var pointsText = string.Join("\n", points.Select((p, i) => $"{i + 1}. {p}"));
        var caseInfo = !string.IsNullOrEmpty(caseId) ? $"\nCase ID: {caseId}" : "";

        return "You are drafting official correspondence for a workplace safety organization.\n\n" +
               $"Context from safety policies:\n{context}\n\n" +
               $"Purpose: {purpose}{caseInfo}\n\n" +
               $"Key points to include:\n{pointsText}\n\n" +
               "Instructions:\n" +
               "- Generate a professional business letter\n" +
               "- Use neutral, respectful tone appropriate for workplace safety communications\n" +
               "- Include placeholders for variable information using {placeholder_name} format\n" +
               "- Reference relevant safety policies when applicable\n" +
               "- Keep the tone constructive and solution-focused\n" +
               "- Structure: Subject line, formal greeting, body paragraphs, closing\n\n" +
               "Generate the letter in this JSON format:\n" +
               "{\n" +
               "  \"subject\": \"Subject line here\",\n" +
               "  \"body\": \"Letter body with {placeholders} where needed\",\n" +
               "  \"placeholders\": [\"list\", \"of\", \"placeholder\", \"names\"]\n" +
               "}\n\n" +
               "Response:";
    }

    private async Task<string> CallAzureOpenAIAsync(string prompt, CancellationToken cancellationToken)
    {
        // For demo mode, return a mock response
        if (IsKernelInDemoMode())
        {
            await Task.Delay(100, cancellationToken);
            return GenerateDemoResponse(prompt);
        }

        var result = await Kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        return result.ToString();
    }

    private bool IsKernelInDemoMode()
    {
        return false;
    }

    private string GenerateDemoResponse(string prompt)
    {
        if (prompt.Contains("JSON format"))
        {
            return """
            {
              "subject": "Workplace Safety Protocol Clarification",
              "body": "Dear {{recipient_name}},\n\nI am writing regarding {{case_reference}} to provide clarification on the workplace safety protocols that apply to your situation.\n\nBased on our safety guidelines, the following requirements apply:\n\n1. Personal protective equipment must be worn at all times in designated areas [#1]\n2. All incidents must be reported within 24 hours through proper channels [#2]\n3. Return-to-work procedures require medical clearance as outlined in Policy {{policy_number}} [#3]\n\nIf you have any questions about these requirements, please contact our safety coordinator at {{coordinator_contact}}.\n\nSincerely,\n{{sender_name}}\n{{sender_title}}",
              "placeholders": ["recipient_name", "case_reference", "policy_number", "coordinator_contact", "sender_name", "sender_title"]
            }
            """;
        }
        else
        {
            return "Based on the workplace safety guidelines, all workers must wear appropriate personal protective equipment including hard hats, safety glasses, and steel-toed boots when working in construction areas [#1]. Any workplace incidents must be reported within 24 hours to the safety coordinator [#2]. For return-to-work situations, employees must provide medical clearance and may require gradual accommodations [#3].";
        }
    }

    private string FormatAnswerWithCitations(string response, List<Citation> citations)
    {
        var formattedResponse = response;
        
        for (int i = 0; i < citations.Count && i < 10; i++)
        {
            var citationMarker = $"[#{i + 1}]";
            if (!formattedResponse.Contains(citationMarker))
            {
                var sentences = formattedResponse.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (sentences.Length > i)
                {
                    formattedResponse = formattedResponse.Replace(sentences[i] + ".", sentences[i] + $" {citationMarker}.");
                }
            }
        }

        return formattedResponse;
    }

    private LetterDraft ParseLetterResponse(string response)
    {
        try
        {
            var cleanResponse = response.Trim();
            if (cleanResponse.StartsWith("```json"))
            {
                var startIndex = cleanResponse.IndexOf('\n') + 1;
                var endIndex = cleanResponse.LastIndexOf("```");
                cleanResponse = cleanResponse.Substring(startIndex, endIndex - startIndex).Trim();
            }

            var jsonDoc = System.Text.Json.JsonDocument.Parse(cleanResponse);
            var root = jsonDoc.RootElement;

            var subject = root.TryGetProperty("subject", out var subjectProp) ? subjectProp.GetString() ?? "" : "";
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
            var placeholders = new List<string>();

            if (root.TryGetProperty("placeholders", out var placeholdersProp) && placeholdersProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                placeholders = placeholdersProp.EnumerateArray()
                    .Where(p => p.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(p => p.GetString() ?? "")
                    .ToList();
            }

            return LetterDraft.Create(subject, body, placeholders);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse letter response as JSON, using fallback");
            
            return LetterDraft.Create(
                "Workplace Safety Communication",
                response,
                ["recipient_name", "sender_name"]
            );
        }
    }

    private string? GetParameterValue(Dictionary<string, object> parameters, string key)
    {
        return parameters.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
