using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.ValueObjects;
using System.Text.RegularExpressions;

namespace OHS.Copilot.Infrastructure.Agents;

public class CiteCheckerAgent : BaseAgent
{
    public override string Name => "CiteChecker";

    public CiteCheckerAgent(Kernel kernel, ILogger<CiteCheckerAgent> logger, ITelemetryService? telemetryService = null) : base(kernel, logger, telemetryService)
    {
    }

    protected override async Task<AgentResult> ExecuteInternalAsync(AgentContext context, CancellationToken cancellationToken)
    {
        var requestType = context.GetData<string>("request_type");

        return requestType switch
        {
            "ask" => await ValidateAnswerCitationsAsync(context, cancellationToken),
            "draft" => await ValidateLetterPolicyReferencesAsync(context, cancellationToken),
            _ => AgentResult.Successful(new Dictionary<string, object> { ["validation"] = "skipped" })
        };
    }

    private async Task<AgentResult> ValidateAnswerCitationsAsync(AgentContext context, CancellationToken cancellationToken)
    {
        var answer = context.GetData<Answer>("answer");
        var citations = context.GetData<List<Citation>>("citations");
        
        if (answer == null || citations == null)
        {
            return AgentResult.Failed("Missing answer or citations data");
        }

        var validationResult = ValidateCitations(answer.Content, citations);
        
        LogAction("validate_citations", new { 
            citationsFound = validationResult.CitationsFound,
            citationsValid = validationResult.CitationsValid,
            coveragePercentage = validationResult.CoveragePercentage,
            hasValidCitations = validationResult.HasValidCitations
        });

        if (!validationResult.HasValidCitations)
        {
            var correctedAnswer = await CorrectCitations(answer.Content, citations, cancellationToken);
            var newAnswer = Answer.Create(correctedAnswer, citations);
            
            context.SetData("answer", newAnswer);
            
            LogAction("correct_citations", new { 
                originalLength = answer.Content.Length,
                correctedLength = correctedAnswer.Length 
            });
        }

        context.SetData("citation_validation", validationResult);

        return AgentResult.Successful(new Dictionary<string, object>
        {
            ["validation_result"] = validationResult,
            ["citations_valid"] = validationResult.HasValidCitations
        });
    }

    private async Task<AgentResult> ValidateLetterPolicyReferencesAsync(AgentContext context, CancellationToken cancellationToken)
    {
        var letterDraft = context.GetData<LetterDraft>("letter_draft");
        
        if (letterDraft == null)
        {
            return AgentResult.Failed("Missing letter draft data");
        }

        var policyReferences = ExtractPolicyReferences(letterDraft.Body);
        var validationResult = new PolicyValidationResult
        {
            PolicyReferencesFound = policyReferences.Count,
            HasPolicyReferences = policyReferences.Count > 0,
            PolicyReferences = policyReferences
        };

        LogAction("validate_policy_references", new { 
            referencesFound = policyReferences.Count,
            hasReferences = validationResult.HasPolicyReferences 
        });

        context.SetData("policy_validation", validationResult);

        return AgentResult.Successful(new Dictionary<string, object>
        {
            ["policy_validation"] = validationResult
        });
    }

    private CitationValidationResult ValidateCitations(string content, List<Citation> citations)
    {
        var citationPattern = @"\[#(\d+)\]";
        var matches = Regex.Matches(content, citationPattern);
        
        var foundCitationNumbers = matches.Cast<Match>()
            .Select(m => int.TryParse(m.Groups[1].Value, out var num) ? num : 0)
            .Where(num => num > 0)
            .Distinct()
            .ToList();

        var paragraphs = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var paragraphsWithCitations = 0;
        foreach (var paragraph in paragraphs)
        {
            if (Regex.IsMatch(paragraph, citationPattern))
            {
                paragraphsWithCitations++;
            }
        }

        var coveragePercentage = paragraphs.Count > 0 
            ? (double)paragraphsWithCitations / paragraphs.Count * 100 
            : 0;

        var validCitations = foundCitationNumbers.All(num => num <= citations.Count);

        return new CitationValidationResult
        {
            CitationsFound = foundCitationNumbers.Count,
            CitationsValid = validCitations,
            CoveragePercentage = coveragePercentage,
            HasValidCitations = foundCitationNumbers.Count > 0 && validCitations && coveragePercentage >= 80,
            FoundCitationNumbers = foundCitationNumbers,
            ParagraphCount = paragraphs.Count,
            ParagraphsWithCitations = paragraphsWithCitations
        };
    }

    private async Task<string> CorrectCitations(string content, List<Citation> citations, CancellationToken cancellationToken)
    {
        var sentences = content.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var correctedSentences = new List<string>();

        for (int i = 0; i < sentences.Length; i++)
        {
            var sentence = sentences[i].Trim();
            
            if (!Regex.IsMatch(sentence, @"\[#\d+\]") && i < citations.Count)
            {
                sentence = $"{sentence} [#{i + 1}]";
            }
            
            correctedSentences.Add(sentence);
        }

        await Task.CompletedTask;
        return string.Join(". ", correctedSentences) + ".";
    }

    private List<string> ExtractPolicyReferences(string content)
    {
        var policyPatterns = new[]
        {
            @"Policy\s+(\d+(?:\.\d+)?)",
            @"Section\s+(\d+(?:\.\d+)?)",
            @"Regulation\s+(\d+(?:\.\d+)?)",
            @"Form\s+([A-Z0-9]+)",
            @"Procedure\s+([A-Z0-9-]+)"
        };

        var references = new HashSet<string>();

        foreach (var pattern in policyPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                references.Add(match.Value);
            }
        }

        return references.ToList();
    }

    private string? GetParameterValue(Dictionary<string, object> parameters, string key)
    {
        return parameters.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}

public class CitationValidationResult
{
    public int CitationsFound { get; set; }
    public bool CitationsValid { get; set; }
    public double CoveragePercentage { get; set; }
    public bool HasValidCitations { get; set; }
    public List<int> FoundCitationNumbers { get; set; } = [];
    public int ParagraphCount { get; set; }
    public int ParagraphsWithCitations { get; set; }
}

public class PolicyValidationResult
{
    public int PolicyReferencesFound { get; set; }
    public bool HasPolicyReferences { get; set; }
    public List<string> PolicyReferences { get; set; } = [];
}
