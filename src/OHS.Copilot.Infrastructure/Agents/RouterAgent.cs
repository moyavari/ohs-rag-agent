using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;

namespace OHS.Copilot.Infrastructure.Agents;

public class RouterAgent : BaseAgent
{
    private readonly IMemoryService _memoryService;

    public override string Name => "Router";

    public RouterAgent(Kernel kernel, ILogger<RouterAgent> logger, IMemoryService memoryService, ITelemetryService? telemetryService = null) : base(kernel, logger, telemetryService)
    {
        _memoryService = memoryService;
    }

    protected override async Task<AgentResult> ExecuteInternalAsync(AgentContext context, CancellationToken cancellationToken)
    {
        var request = context.GetData<object>("request");
        
        if (request == null)
        {
            return AgentResult.Failed("No request data provided");
        }

        var requestType = DetermineRequestType(request);
        var parameters = ExtractParameters(request, requestType);

        var conversationId = context.ConversationId;
        if (!string.IsNullOrEmpty(conversationId))
        {
            var conversationMemory = await _memoryService.GetConversationMemoryAsync(conversationId, cancellationToken);
            if (conversationMemory != null)
            {
                context.SetData("conversation_memory", conversationMemory);
                LogAction("load_conversation_memory", new { conversationId, turnCount = conversationMemory.Turns.Count });
            }
        }

        var userId = parameters.ContainsKey("UserId") ? parameters["UserId"]?.ToString() : null;
        if (!string.IsNullOrEmpty(userId))
        {
            var personaMemory = await _memoryService.GetPersonaMemoryAsync(userId, cancellationToken);
            if (personaMemory != null)
            {
                context.SetData("persona_memory", personaMemory);
                LogAction("load_persona_memory", new { userId, personaType = personaMemory.Type });
            }
        }

        LogAction("classify_request", new { requestType, parameterCount = parameters.Count });

        context.SetData("request_type", requestType);
        context.SetData("parameters", parameters);

        return AgentResult.Successful(new Dictionary<string, object>
        {
            ["request_type"] = requestType,
            ["parameters"] = parameters
        });
    }

    private string DetermineRequestType(object request)
    {
        var requestTypeName = request.GetType().Name;
        
        return requestTypeName switch
        {
            "AskRequest" => "ask",
            "DraftLetterRequest" => "draft",
            "IngestRequest" => "ingest",
            _ => "unknown"
        };
    }

    private Dictionary<string, object> ExtractParameters(object request, string requestType)
    {
        var parameters = new Dictionary<string, object>();

        try
        {
            var properties = request.GetType().GetProperties();
            
            foreach (var prop in properties)
            {
                var value = prop.GetValue(request);
                if (value != null)
                {
                    parameters[prop.Name] = value;
                }
            }

            LogAction("extract_parameters", new { requestType, extractedCount = parameters.Count });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to extract parameters from {RequestType}", requestType);
        }

        return parameters;
    }

    public bool IsValidRequestType(string requestType)
    {
        return requestType is "ask" or "draft" or "ingest";
    }
}
