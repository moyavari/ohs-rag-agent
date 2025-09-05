namespace OHS.Copilot.Application.Interfaces;

public interface IContentModerationService
{
    Task<ModerationResult> ModerateTextAsync(string text, bool moderateInput = true, bool moderateOutput = true, CancellationToken cancellationToken = default);
    Task<List<ModerationResult>> ModerateBatchAsync(IEnumerable<string> texts, bool moderateInput = true, bool moderateOutput = true, CancellationToken cancellationToken = default);
    bool IsModerationEnabled();
}

public class ModerationResult
{
    public string Content { get; set; } = string.Empty;
    public bool Flagged { get; set; }
    public ModerationAction Action { get; set; } = ModerationAction.Allow;
    public List<ModerationCategory> Categories { get; set; } = [];
    public string? Reason { get; set; }
    public double OverallSeverity { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ModerationResult Safe(string content)
    {
        return new ModerationResult
        {
            Content = content,
            Flagged = false,
            Action = ModerationAction.Allow,
            OverallSeverity = 0.0
        };
    }

    public static ModerationResult CreateFlagged(string content, List<ModerationCategory> categories, ModerationAction action, string? reason = null)
    {
        return new ModerationResult
        {
            Content = content,
            Flagged = true,
            Action = action,
            Categories = categories,
            Reason = reason,
            OverallSeverity = categories.Count > 0 ? categories.Max(c => c.Severity) : 0.0
        };
    }
}

public class ModerationCategory
{
    public string Name { get; set; } = string.Empty;
    public double Severity { get; set; }
    public SeverityLevel Level { get; set; } = SeverityLevel.Safe;
}

public enum ModerationAction
{
    Allow,
    AllowWithWarning,
    Block
}

public enum SeverityLevel
{
    Safe = 0,
    Low = 2,
    Medium = 4,
    High = 6
}
