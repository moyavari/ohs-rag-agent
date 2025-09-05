using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Application.DTOs.Requests;
using OHS.Copilot.Application.DTOs.Responses;
using OHS.Copilot.Domain.ValueObjects;
using OHS.Copilot.Domain.Entities;

namespace OHS.Copilot.Application.Services;

public class AgentOrchestrationService
{
    private readonly IAgent _routerAgent;
    private readonly IAgent _retrieverAgent;
    private readonly IAgent _drafterAgent;
    private readonly IAgent _citeCheckerAgent;
    private readonly IAuditService _auditService;
    private readonly IPromptVersionService _promptVersionService;
    private readonly IContentRedactionService _redactionService;
    private readonly IContentModerationService _moderationService;
    private readonly IMemoryService _memoryService;
    private readonly IDemoModeService? _demoModeService;
    private readonly ILogger<AgentOrchestrationService> _logger;

    public AgentOrchestrationService(
        IEnumerable<IAgent> agents,
        IAuditService auditService,
        IPromptVersionService promptVersionService,
        IContentRedactionService redactionService,
        IContentModerationService moderationService,
        IMemoryService memoryService,
        ILogger<AgentOrchestrationService> logger,
        IDemoModeService? demoModeService = null)
    {
        var agentList = agents.ToList();
        
        _routerAgent = agentList.First(a => a.Name == "Router");
        _retrieverAgent = agentList.First(a => a.Name == "Retriever");
        _drafterAgent = agentList.First(a => a.Name == "Drafter");
        _citeCheckerAgent = agentList.First(a => a.Name == "CiteChecker");
        
        _auditService = auditService;
        _promptVersionService = promptVersionService;
        _redactionService = redactionService;
        _moderationService = moderationService;
        _memoryService = memoryService;
        _demoModeService = demoModeService;
        _logger = logger;
    }

    public async Task<AskResponse> ProcessAskRequestAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        if (_demoModeService?.IsDemoModeEnabled() == true)
        {
            var demoResponse = await _demoModeService.GetDemoAskResponseAsync(request, cancellationToken);
            if (demoResponse != null)
            {
                _logger.LogInformation("Returning demo mode fixture response for question");
                return demoResponse;
            }
        }

        var context = new AgentContext
        {
            ConversationId = request.ConversationId
        };
        
        context.SetData("request", request);

        var auditId = await CreateAuditLogAsync("ask", context.CorrelationId, request, cancellationToken);
        context.SetData("audit_id", auditId);

        _logger.LogInformation("Starting Ask pipeline for correlation {CorrelationId}, audit {AuditId}", 
            context.CorrelationId, auditId);

        try
        {
            var inputModerationResult = await _moderationService.ModerateTextAsync(request.Question, moderateInput: true, cancellationToken: cancellationToken);
            await _auditService.SetModerationResultAsync(auditId, 
                new Dictionary<string, object> { ["input_moderation"] = inputModerationResult }, cancellationToken);

            if (inputModerationResult.Action == ModerationAction.Block)
            {
                throw new InvalidOperationException("Input content was blocked by content safety policies");
            }

            var routerResult = await _routerAgent.ExecuteAsync(context, cancellationToken);
            if (!routerResult.Success)
            {
                throw new InvalidOperationException($"Router failed: {routerResult.ErrorMessage}");
            }
            await LogAgentTrace(auditId, "Router", context.Traces.LastOrDefault(), cancellationToken);

            var retrieverResult = await _retrieverAgent.ExecuteAsync(context, cancellationToken);
            if (!retrieverResult.Success)
            {
                throw new InvalidOperationException($"Retriever failed: {retrieverResult.ErrorMessage}");
            }
            await LogAgentTrace(auditId, "Retriever", context.Traces.LastOrDefault(), cancellationToken);

            var drafterResult = await _drafterAgent.ExecuteAsync(context, cancellationToken);
            if (!drafterResult.Success)
            {
                throw new InvalidOperationException($"Drafter failed: {drafterResult.ErrorMessage}");
            }
            await LogAgentTrace(auditId, "Drafter", context.Traces.LastOrDefault(), cancellationToken);

            var citeCheckerResult = await _citeCheckerAgent.ExecuteAsync(context, cancellationToken);
            if (!citeCheckerResult.Success)
            {
                _logger.LogWarning("CiteChecker failed, using unchecked response: {Error}", citeCheckerResult.ErrorMessage);
            }
            await LogAgentTrace(auditId, "CiteChecker", context.Traces.LastOrDefault(), cancellationToken);

            var answer = context.GetData<Answer>("answer");
            var promptHash = context.GetData<string>("prompt_hash");
            
            if (answer == null)
            {
                throw new InvalidOperationException("No answer generated by pipeline");
            }

            var outputModerationResult = await _moderationService.ModerateTextAsync(answer.Content, moderateOutput: true, cancellationToken: cancellationToken);
            if (outputModerationResult.Action == ModerationAction.Block)
            {
                throw new InvalidOperationException("Generated content was blocked by content safety policies");
            }

            var redactionResult = await _redactionService.RedactContentAsync(answer.Content, cancellationToken: cancellationToken);
            var finalAnswer = redactionResult.HasRedactions 
                ? Answer.Create(redactionResult.RedactedContent, answer.Citations)
                : answer;

            var response = AskResponse.FromAnswer(finalAnswer, request.ConversationId);
            response.Metadata.PromptSha = promptHash ?? "";
            response.Metadata.ProcessingTime = TimeSpan.FromMilliseconds(context.Traces.Sum(t => t.Duration.TotalMilliseconds));
            response.Metadata.AgentTraces = CreateAgentTracesSummary(context.Traces);

            if (outputModerationResult.Flagged)
            {
                response.Metadata.ModerationResult["output_moderation"] = outputModerationResult;
            }

            await _auditService.UpdateRequestAsync(auditId, 
                new Dictionary<string, object> { ["response"] = response },
                answer.Citations.Select(c => c.Id).ToList(),
                cancellationToken);

            if (!string.IsNullOrEmpty(request.ConversationId))
            {
                await _memoryService.UpdateConversationMemoryAsync(
                    request.ConversationId, 
                    request.Question, 
                    finalAnswer.Content, 
                    cancellationToken);
                
                _logger.LogDebug("Updated conversation memory for {ConversationId}", request.ConversationId);
            }

            _logger.LogInformation("Ask pipeline completed successfully for correlation {CorrelationId}", context.CorrelationId);

            return response;
        }
        catch (Exception ex)
        {
            await _auditService.UpdateRequestAsync(auditId, 
                new Dictionary<string, object> { ["error"] = ex.Message }, 
                [], cancellationToken);
                
            _logger.LogError(ex, "Ask pipeline failed for correlation {CorrelationId}", context.CorrelationId);
            throw;
        }
    }

    public async Task<DraftLetterResponse> ProcessDraftLetterRequestAsync(DraftLetterRequest request, CancellationToken cancellationToken = default)
    {
        if (_demoModeService?.IsDemoModeEnabled() == true)
        {
            var demoResponse = await _demoModeService.GetDemoLetterResponseAsync(request, cancellationToken);
            if (demoResponse != null)
            {
                _logger.LogInformation("Returning demo mode fixture response for letter");
                return demoResponse;
            }
        }

        var context = new AgentContext
        {
            ConversationId = request.ConversationId
        };
        
        context.SetData("request", request);

        var auditId = await CreateAuditLogAsync("draft", context.CorrelationId, request, cancellationToken);
        context.SetData("audit_id", auditId);

        _logger.LogInformation("Starting Draft pipeline for correlation {CorrelationId}, audit {AuditId}", 
            context.CorrelationId, auditId);

        try
        {
            var routerResult = await _routerAgent.ExecuteAsync(context, cancellationToken);
            if (!routerResult.Success)
            {
                throw new InvalidOperationException($"Router failed: {routerResult.ErrorMessage}");
            }
            await LogAgentTrace(auditId, "Router", context.Traces.LastOrDefault(), cancellationToken);

            var retrieverResult = await _retrieverAgent.ExecuteAsync(context, cancellationToken);
            if (!retrieverResult.Success)
            {
                throw new InvalidOperationException($"Retriever failed: {retrieverResult.ErrorMessage}");
            }
            await LogAgentTrace(auditId, "Retriever", context.Traces.LastOrDefault(), cancellationToken);

            var drafterResult = await _drafterAgent.ExecuteAsync(context, cancellationToken);
            if (!drafterResult.Success)
            {
                throw new InvalidOperationException($"Drafter failed: {drafterResult.ErrorMessage}");
            }
            await LogAgentTrace(auditId, "Drafter", context.Traces.LastOrDefault(), cancellationToken);

            var citeCheckerResult = await _citeCheckerAgent.ExecuteAsync(context, cancellationToken);
            if (!citeCheckerResult.Success)
            {
                _logger.LogWarning("CiteChecker failed for letter, using unchecked draft: {Error}", citeCheckerResult.ErrorMessage);
            }
            await LogAgentTrace(auditId, "CiteChecker", context.Traces.LastOrDefault(), cancellationToken);

            var letterDraft = context.GetData<LetterDraft>("letter_draft");
            var promptHash = context.GetData<string>("prompt_hash");
            
            if (letterDraft == null)
            {
                throw new InvalidOperationException("No letter draft generated by pipeline");
            }

            var outputModerationResult = await _moderationService.ModerateTextAsync(letterDraft.Body, moderateOutput: true, cancellationToken: cancellationToken);
            if (outputModerationResult.Action == ModerationAction.Block)
            {
                throw new InvalidOperationException("Generated letter was blocked by content safety policies");
            }

            var redactionResult = await _redactionService.RedactContentAsync(letterDraft.Body, cancellationToken: cancellationToken);
            var finalDraft = redactionResult.HasRedactions 
                ? LetterDraft.Create(letterDraft.Subject, redactionResult.RedactedContent, letterDraft.Placeholders)
                : letterDraft;

            var response = DraftLetterResponse.FromLetterDraft(finalDraft, request.ConversationId);
            response.Metadata.PromptSha = promptHash ?? "";
            response.Metadata.ProcessingTime = TimeSpan.FromMilliseconds(context.Traces.Sum(t => t.Duration.TotalMilliseconds));
            response.Metadata.AgentTraces = CreateAgentTracesSummary(context.Traces);

            if (outputModerationResult.Flagged)
            {
                response.Metadata.ModerationResult["output_moderation"] = outputModerationResult;
            }

            await _auditService.UpdateRequestAsync(auditId,
                new Dictionary<string, object> { ["response"] = response },
                [],
                cancellationToken);

            if (!string.IsNullOrEmpty(request.ConversationId))
            {
                var purpose = request.Purpose;
                var letterSummary = $"Generated letter: {finalDraft.Subject}";
                
                await _memoryService.UpdateConversationMemoryAsync(
                    request.ConversationId,
                    purpose,
                    letterSummary,
                    cancellationToken);
                
                _logger.LogDebug("Updated conversation memory for draft letter {ConversationId}", request.ConversationId);
            }

            _logger.LogInformation("Draft pipeline completed successfully for correlation {CorrelationId}", context.CorrelationId);

            return response;
        }
        catch (Exception ex)
        {
            await _auditService.UpdateRequestAsync(auditId,
                new Dictionary<string, object> { ["error"] = ex.Message },
                [], cancellationToken);
                
            _logger.LogError(ex, "Draft pipeline failed for correlation {CorrelationId}", context.CorrelationId);
            throw;
        }
    }

    private Dictionary<string, object> CreateAgentTracesSummary(List<AgentTrace> traces)
    {
        var summary = new Dictionary<string, object>
        {
            ["total_agents"] = traces.Select(t => t.AgentName).Distinct().Count(),
            ["total_duration_ms"] = traces.Sum(t => t.Duration.TotalMilliseconds),
            ["traces"] = traces.Select(t => new
            {
                agent = t.AgentName,
                action = t.Action,
                duration_ms = t.Duration.TotalMilliseconds,
                timestamp = t.Timestamp,
                data = t.Data
            }).ToList()
        };

        return summary;
    }

    private async Task<string> CreateAuditLogAsync(string operation, string correlationId, object request, CancellationToken cancellationToken)
    {
        var auditEntry = AuditLogEntry.Create(
            operation: operation,
            promptSha: "PENDING",  // Will be updated when prompt is generated
            model: "demo-model",
            userId: null  // Would be extracted from JWT in production
        );

        auditEntry.Inputs["request"] = request;
        auditEntry.Inputs["correlation_id"] = correlationId;

        return await _auditService.LogRequestAsync(auditEntry, cancellationToken);
    }

    private async Task LogAgentTrace(string auditId, string agentName, AgentTrace? trace, CancellationToken cancellationToken)
    {
        if (trace != null)
        {
            await _auditService.AddAgentTraceAsync(auditId, agentName, trace.Action, trace.Data, trace.Duration, cancellationToken);
        }
    }
}
