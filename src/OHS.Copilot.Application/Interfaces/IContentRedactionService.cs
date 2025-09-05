namespace OHS.Copilot.Application.Interfaces;

public interface IContentRedactionService
{
    Task<RedactionResult> RedactContentAsync(string content, RedactionOptions? options = null, CancellationToken cancellationToken = default);
    Task<List<RedactionResult>> RedactBatchAsync(IEnumerable<string> contents, RedactionOptions? options = null, CancellationToken cancellationToken = default);
    bool IsRedactionEnabled();
}

public class RedactionResult
{
    public string OriginalContent { get; set; } = string.Empty;
    public string RedactedContent { get; set; } = string.Empty;
    public List<RedactionMatch> Redactions { get; set; } = [];
    public bool HasRedactions => Redactions.Count > 0;

    public static RedactionResult Create(string originalContent, string redactedContent, List<RedactionMatch> redactions)
    {
        return new RedactionResult
        {
            OriginalContent = originalContent,
            RedactedContent = redactedContent,
            Redactions = redactions
        };
    }

    public static RedactionResult NoRedaction(string content)
    {
        return new RedactionResult
        {
            OriginalContent = content,
            RedactedContent = content,
            Redactions = []
        };
    }
}

public class RedactionMatch
{
    public string Type { get; set; } = string.Empty;
    public string OriginalValue { get; set; } = string.Empty;
    public string RedactedValue { get; set; } = string.Empty;
    public int StartPosition { get; set; }
    public int Length { get; set; }

    public static RedactionMatch Create(string type, string originalValue, string redactedValue, int startPosition)
    {
        return new RedactionMatch
        {
            Type = type,
            OriginalValue = originalValue,
            RedactedValue = redactedValue,
            StartPosition = startPosition,
            Length = originalValue.Length
        };
    }
}

public class RedactionOptions
{
    public bool RedactPhoneNumbers { get; set; } = true;
    public bool RedactEmailAddresses { get; set; } = true;
    public bool RedactSocialSecurityNumbers { get; set; } = true;
    public bool RedactCreditCardNumbers { get; set; } = true;
    public bool RedactNames { get; set; } = false;
    public bool RedactAddresses { get; set; } = false;
    public List<RedactionRule> CustomRules { get; set; } = [];

    public static RedactionOptions Default()
    {
        return new RedactionOptions();
    }

    public static RedactionOptions Conservative()
    {
        return new RedactionOptions
        {
            RedactPhoneNumbers = true,
            RedactEmailAddresses = true,
            RedactSocialSecurityNumbers = true,
            RedactCreditCardNumbers = true,
            RedactNames = false,
            RedactAddresses = false
        };
    }

    public static RedactionOptions Aggressive()
    {
        return new RedactionOptions
        {
            RedactPhoneNumbers = true,
            RedactEmailAddresses = true,
            RedactSocialSecurityNumbers = true,
            RedactCreditCardNumbers = true,
            RedactNames = true,
            RedactAddresses = true
        };
    }
}

public class RedactionRule
{
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
