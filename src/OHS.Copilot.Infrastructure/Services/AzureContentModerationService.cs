using Azure;
using Azure.AI.ContentSafety;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Configuration;

namespace OHS.Copilot.Infrastructure.Services;

public class AzureContentModerationService : IContentModerationService
{
    private readonly ContentSafetyClient? _client;
    private readonly AppSettings _settings;
    private readonly ILogger<AzureContentModerationService> _logger;
    private readonly SeverityLevel _threshold;

    public AzureContentModerationService(AppSettings settings, ILogger<AzureContentModerationService> logger)
    {
        _settings = settings;
        _logger = logger;

        if (!string.IsNullOrEmpty(settings.ContentSafety.Endpoint) && !string.IsNullOrEmpty(settings.ContentSafety.Key))
        {
            try
            {
                _client = new ContentSafetyClient(
                    new Uri(settings.ContentSafety.Endpoint),
                    new AzureKeyCredential(settings.ContentSafety.Key));
                
                _logger.LogInformation("Azure Content Safety client initialized");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Azure Content Safety client");
            }
        }

        _threshold = ParseSeverityThreshold(settings.ContentSafety.Threshold);
    }

    public bool IsModerationEnabled()
    {
        return _client != null && !string.IsNullOrEmpty(_settings.ContentSafety.Endpoint);
    }

    public async Task<ModerationResult> ModerateTextAsync(string text, bool moderateInput = true, bool moderateOutput = true, CancellationToken cancellationToken = default)
    {
        if (!IsModerationEnabled() || string.IsNullOrEmpty(text))
        {
            return ModerationResult.Safe(text);
        }

        try
        {
            _logger.LogDebug("Moderating content of length {Length}", text.Length);

            var request = new AnalyzeTextOptions(text);
            var response = await _client!.AnalyzeTextAsync(request, cancellationToken);
            
            var result = ProcessModerationResponse(text, response.Value);
            
            _logger.LogDebug("Content moderation completed: Flagged={Flagged}, Action={Action}, Severity={Severity}",
                result.Flagged, result.Action, result.OverallSeverity);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content moderation failed, allowing content");
            return ModerationResult.Safe(text);
        }
    }

    public async Task<List<ModerationResult>> ModerateBatchAsync(IEnumerable<string> texts, bool moderateInput = true, bool moderateOutput = true, CancellationToken cancellationToken = default)
    {
        var tasks = texts.Select(text => ModerateTextAsync(text, moderateInput, moderateOutput, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private ModerationResult ProcessModerationResponse(string content, AnalyzeTextResult response)
    {
        var categories = new List<ModerationCategory>();
        var flagged = false;
        var action = ModerationAction.Allow;
        var maxSeverity = 0.0;

        if (response.CategoriesAnalysis != null)
        {
            foreach (var category in response.CategoriesAnalysis)
            {
                var severityInt = category.Severity ?? 0;
                var severityLevel = ConvertSeverityLevel(severityInt);
                var severityValue = (double)severityInt;
                
                categories.Add(new ModerationCategory
                {
                    Name = category.Category.ToString(),
                    Severity = severityValue,
                    Level = severityLevel
                });

                if (severityValue > maxSeverity)
                {
                    maxSeverity = severityValue;
                }

                if (severityInt >= (int)_threshold)
                {
                    flagged = true;
                    action = DetermineAction(severityLevel);
                }
            }
        }

        if (flagged)
        {
            var flaggedCategories = categories.Where(c => c.Level >= _threshold).ToList();
            var reason = $"Content flagged for: {string.Join(", ", flaggedCategories.Select(c => c.Name))}";
            
            return ModerationResult.CreateFlagged(content, categories, action, reason);
        }

        return ModerationResult.Safe(content);
    }

    private SeverityLevel ConvertSeverityLevel(int severity)
    {
        return severity switch
        {
            0 => SeverityLevel.Safe,
            2 => SeverityLevel.Low,
            4 => SeverityLevel.Medium,
            6 => SeverityLevel.High,
            _ => SeverityLevel.Safe
        };
    }

    private ModerationAction DetermineAction(SeverityLevel level)
    {
        return level switch
        {
            SeverityLevel.High => ModerationAction.Block,
            SeverityLevel.Medium => ModerationAction.AllowWithWarning,
            SeverityLevel.Low => ModerationAction.AllowWithWarning,
            _ => ModerationAction.Allow
        };
    }

    private SeverityLevel ParseSeverityThreshold(string threshold)
    {
        return threshold.ToLower() switch
        {
            "low" => SeverityLevel.Low,
            "medium" => SeverityLevel.Medium,
            "high" => SeverityLevel.High,
            _ => SeverityLevel.Medium
        };
    }
}

public class DemoContentModerationService : IContentModerationService
{
    private readonly ILogger<DemoContentModerationService> _logger;
    private readonly Random _random = new(42);

    public DemoContentModerationService(ILogger<DemoContentModerationService> logger)
    {
        _logger = logger;
    }

    public bool IsModerationEnabled() => true;

    public Task<ModerationResult> ModerateTextAsync(string text, bool moderateInput = true, bool moderateOutput = true, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Demo content moderation for text of length {Length}", text.Length);

        var shouldFlag = text.ToLower().Contains("unsafe") || text.ToLower().Contains("dangerous");
        
        if (shouldFlag)
        {
            var categories = new List<ModerationCategory>
            {
                new() { Name = "Violence", Severity = 4.0, Level = SeverityLevel.Medium }
            };
            
            return Task.FromResult(ModerationResult.CreateFlagged(text, categories, ModerationAction.AllowWithWarning, "Demo flagging for testing"));
        }

        return Task.FromResult(ModerationResult.Safe(text));
    }

    public async Task<List<ModerationResult>> ModerateBatchAsync(IEnumerable<string> texts, bool moderateInput = true, bool moderateOutput = true, CancellationToken cancellationToken = default)
    {
        var tasks = texts.Select(text => ModerateTextAsync(text, moderateInput, moderateOutput, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}
