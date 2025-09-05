using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Configuration;
using System.Text.RegularExpressions;

namespace OHS.Copilot.Infrastructure.Services;

public class ContentRedactionService : IContentRedactionService
{
    private readonly AppSettings _settings;
    private readonly ILogger<ContentRedactionService> _logger;
    private readonly Dictionary<string, Regex> _redactionPatterns;

    public ContentRedactionService(AppSettings settings, ILogger<ContentRedactionService> logger)
    {
        _settings = settings;
        _logger = logger;
        _redactionPatterns = InitializeRedactionPatterns();
    }

    public bool IsRedactionEnabled()
    {
        return _settings.ContentSafety.RedactionEnabled;
    }

    public Task<RedactionResult> RedactContentAsync(string content, RedactionOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!IsRedactionEnabled() || string.IsNullOrEmpty(content))
        {
            return Task.FromResult(RedactionResult.NoRedaction(content));
        }

        options ??= RedactionOptions.Default();
        
        _logger.LogDebug("Starting content redaction for text of length {Length}", content.Length);

        var redactions = new List<RedactionMatch>();
        var redactedContent = content;

        if (options.RedactPhoneNumbers)
        {
            var phoneRedactions = RedactPattern(redactedContent, "PhoneNumber", "[PHONE-REDACTED]");
            redactions.AddRange(phoneRedactions.redactions);
            redactedContent = phoneRedactions.content;
        }

        if (options.RedactEmailAddresses)
        {
            var emailRedactions = RedactPattern(redactedContent, "EmailAddress", "[EMAIL-REDACTED]");
            redactions.AddRange(emailRedactions.redactions);
            redactedContent = emailRedactions.content;
        }

        if (options.RedactSocialSecurityNumbers)
        {
            var ssnRedactions = RedactPattern(redactedContent, "SocialSecurityNumber", "[SSN-REDACTED]");
            redactions.AddRange(ssnRedactions.redactions);
            redactedContent = ssnRedactions.content;
        }

        if (options.RedactCreditCardNumbers)
        {
            var ccRedactions = RedactPattern(redactedContent, "CreditCardNumber", "[CC-REDACTED]");
            redactions.AddRange(ccRedactions.redactions);
            redactedContent = ccRedactions.content;
        }

        foreach (var customRule in options.CustomRules.Where(r => r.Enabled))
        {
            var customRedactions = RedactCustomPattern(redactedContent, customRule);
            redactions.AddRange(customRedactions.redactions);
            redactedContent = customRedactions.content;
        }

        _logger.LogDebug("Completed content redaction: {RedactionCount} items redacted", redactions.Count);

        return Task.FromResult(RedactionResult.Create(content, redactedContent, redactions));
    }

    public async Task<List<RedactionResult>> RedactBatchAsync(IEnumerable<string> contents, RedactionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var tasks = contents.Select(content => RedactContentAsync(content, options, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private (string content, List<RedactionMatch> redactions) RedactPattern(string content, string patternType, string replacement)
    {
        if (!_redactionPatterns.TryGetValue(patternType, out var pattern))
        {
            return (content, []);
        }

        var redactions = new List<RedactionMatch>();
        var matches = pattern.Matches(content);

        if (matches.Count == 0)
        {
            return (content, redactions);
        }

        var redactedContent = content;

        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            var redaction = RedactionMatch.Create(
                type: patternType,
                originalValue: match.Value,
                redactedValue: replacement,
                startPosition: match.Index
            );
            
            redactions.Insert(0, redaction);
            redactedContent = redactedContent.Remove(match.Index, match.Length).Insert(match.Index, replacement);
        }

        return (redactedContent, redactions);
    }

    private (string content, List<RedactionMatch> redactions) RedactCustomPattern(string content, RedactionRule rule)
    {
        try
        {
            var pattern = new Regex(rule.Pattern, RegexOptions.IgnoreCase);
            var redactions = new List<RedactionMatch>();
            var matches = pattern.Matches(content);

            if (matches.Count == 0)
            {
                return (content, redactions);
            }

            var redactedContent = content;

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                var redaction = RedactionMatch.Create(
                    type: rule.Name,
                    originalValue: match.Value,
                    redactedValue: rule.Replacement,
                    startPosition: match.Index
                );
                
                redactions.Insert(0, redaction);
                redactedContent = redactedContent.Remove(match.Index, match.Length).Insert(match.Index, rule.Replacement);
            }

            return (redactedContent, redactions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply custom redaction rule {RuleName}", rule.Name);
            return (content, []);
        }
    }

    private Dictionary<string, Regex> InitializeRedactionPatterns()
    {
        var patterns = new Dictionary<string, Regex>();

        try
        {
            patterns["PhoneNumber"] = new Regex(
                @"(\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            patterns["EmailAddress"] = new Regex(
                @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            patterns["SocialSecurityNumber"] = new Regex(
                @"\b(?!000)(?!666)(?!9)\d{3}[-.\s]?(?!00)\d{2}[-.\s]?(?!0000)\d{4}\b",
                RegexOptions.Compiled);

            patterns["CreditCardNumber"] = new Regex(
                @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|3[0-9]{13}|6(?:011|5[0-9]{2})[0-9]{12})\b",
                RegexOptions.Compiled);

            _logger.LogDebug("Initialized {PatternCount} redaction patterns", patterns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize redaction patterns");
        }

        return patterns;
    }

    public async Task<string> GenerateSyntheticPiiAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var syntheticData = new List<string>();
        var random = new Random();

        for (int i = 0; i < count; i++)
        {
            var phoneNumber = $"({random.Next(100, 999)}) {random.Next(100, 999)}-{random.Next(1000, 9999)}";
            var email = $"user{i}@example{random.Next(1, 100)}.com";
            var ssn = $"{random.Next(100, 999)}-{random.Next(10, 99)}-{random.Next(1000, 9999)}";
            
            syntheticData.Add($"Contact info: {phoneNumber}, {email}, SSN: {ssn}");
        }

        var content = string.Join("\n", syntheticData);
        
        _logger.LogDebug("Generated {Count} synthetic PII records for testing", count);
        
        return content;
    }
}
