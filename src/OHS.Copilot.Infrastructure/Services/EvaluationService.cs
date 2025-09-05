using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Application.DTOs.Requests;
using OHS.Copilot.Application.DTOs.Responses;
using OHS.Copilot.Application.Services;

namespace OHS.Copilot.Infrastructure.Services;

public class EvaluationService : IEvaluationService
{
    private readonly AgentOrchestrationService _orchestrationService;
    private readonly IDemoModeService _demoModeService;
    private readonly ILogger<EvaluationService> _logger;

    public EvaluationService(
        AgentOrchestrationService orchestrationService,
        IDemoModeService demoModeService,
        ILogger<EvaluationService> logger)
    {
        _orchestrationService = orchestrationService;
        _demoModeService = demoModeService;
        _logger = logger;
    }

    public async Task<EvaluationReport> RunEvaluationAsync(EvaluationOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= EvaluationOptions.Default();
        
        _logger.LogInformation("Starting evaluation with options: Demo={RunDemo}, Live={RunLive}", 
            options.RunInDemoMode, options.RunInLiveMode);

        var goldenData = await LoadGoldenDatasetAsync(cancellationToken);
        
        if (goldenData.Count == 0)
        {
            throw new InvalidOperationException("No golden dataset loaded for evaluation");
        }

        var report = new EvaluationReport();

        if (options.RunInDemoMode)
        {
            _logger.LogInformation("Running demo mode evaluation with {Count} questions", goldenData.Count);
            var demoResults = await RunEvaluationModeAsync(goldenData, true, options, cancellationToken);
            report.DemoModeMetrics = await CalculateMetricsAsync(demoResults, cancellationToken);
            report.Results.AddRange(demoResults);
            report.Mode = "Demo";
        }

        if (options.RunInLiveMode)
        {
            _logger.LogInformation("Running live mode evaluation with {Count} questions", goldenData.Count);
            var liveResults = await RunEvaluationModeAsync(goldenData, false, options, cancellationToken);
            report.LiveModeMetrics = await CalculateMetricsAsync(liveResults, cancellationToken);
            
            if (report.Mode == "Demo")
            {
                report.Mode = "Both";
            }
            else
            {
                report.Mode = "Live";
                report.Results.AddRange(liveResults);
            }
        }

        report.GenerateSummary();
        
        await SaveEvaluationReportAsync(report, options.ReportOutputPath, cancellationToken);
        
        _logger.LogInformation("Evaluation completed: {SuccessPercentage:F1}% success rate", 
            report.DemoModeMetrics.OverallSuccessPercentage);

        return report;
    }

    public async Task<List<GoldenDataItem>> LoadGoldenDatasetAsync(CancellationToken cancellationToken = default)
    {
        var goldenDataPath = "./tests/golden.csv";
        
        if (!File.Exists(goldenDataPath))
        {
            _logger.LogInformation("Golden dataset not found, creating default dataset at {Path}", goldenDataPath);
            await CreateDefaultGoldenDatasetAsync(goldenDataPath, cancellationToken);
        }

        var goldenData = new List<GoldenDataItem>();

        try
        {
            var lines = await File.ReadAllLinesAsync(goldenDataPath, cancellationToken);
            
            if (lines.Length <= 1)
            {
                throw new InvalidOperationException("Golden dataset file is empty or contains only headers");
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = ParseCsvLine(line);
                if (parts.Length >= 4)
                {
                    var item = GoldenDataItem.Create(
                        id: parts[0],
                        question: parts[1],
                        mustContain: parts[2],
                        mustCiteTitle: parts[3],
                        category: parts.Length > 4 ? parts[4] : "general"
                    );
                    
                    goldenData.Add(item);
                }
            }

            _logger.LogInformation("Loaded {Count} golden dataset items", goldenData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load golden dataset from {Path}", goldenDataPath);
            throw;
        }

        return goldenData;
    }

    public Task<EvaluationMetrics> CalculateMetricsAsync(List<EvaluationResult> results, CancellationToken cancellationToken = default)
    {
        var metrics = new EvaluationMetrics
        {
            TotalQuestions = results.Count,
            SuccessfulResponses = results.Count(r => r.IsSuccessful),
            ResponsesWithCitations = results.Count(r => r.HasCitations),
            ResponsesWithExpectedContent = results.Count(r => r.ContainsExpectedContent),
            ResponsesWithCorrectCitations = results.Count(r => r.CitesExpectedSource),
            ErrorResponses = results.Count(r => !string.IsNullOrEmpty(r.ErrorMessage)),
            AverageResponseTime = results.Count > 0 
                ? TimeSpan.FromMilliseconds(results.Average(r => r.ResponseTime.TotalMilliseconds))
                : TimeSpan.Zero
        };

        var errorsByCategory = results
            .Where(r => !string.IsNullOrEmpty(r.ErrorMessage))
            .GroupBy(r => r.ErrorMessage ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        metrics.ErrorsByCategory = errorsByCategory;

        return Task.FromResult(metrics);
    }

    public async Task SaveEvaluationReportAsync(EvaluationReport report, string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var markdownReport = GenerateMarkdownReport(report);
            
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "./");
            await File.WriteAllTextAsync(filePath, markdownReport, cancellationToken);
            
            _logger.LogInformation("Saved evaluation report to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save evaluation report to {FilePath}", filePath);
            throw;
        }
    }

    private async Task<List<EvaluationResult>> RunEvaluationModeAsync(
        List<GoldenDataItem> goldenData, 
        bool demoMode, 
        EvaluationOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<EvaluationResult>();
        var semaphore = new SemaphoreSlim(options.MaxConcurrentRequests, options.MaxConcurrentRequests);

        var tasks = goldenData.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            
            try
            {
                return await EvaluateQuestionAsync(item, demoMode, options.RequestTimeout, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        results = (await Task.WhenAll(tasks)).ToList();
        
        return results;
    }

    private async Task<EvaluationResult> EvaluateQuestionAsync(
        GoldenDataItem goldenItem,
        bool demoMode,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var result = new EvaluationResult
        {
            QuestionId = goldenItem.Id,
            Question = goldenItem.Question
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var request = new AskRequest
            {
                Question = goldenItem.Question,
                MaxTokens = 500,
                ConversationId = $"eval-{goldenItem.Id}"
            };

            AskResponse response;
            
            if (demoMode && _demoModeService.IsDemoModeEnabled())
            {
                var demoResponse = await _demoModeService.GetDemoAskResponseAsync(request, cts.Token);
                response = demoResponse ?? throw new InvalidOperationException("Demo mode failed to return response");
            }
            else
            {
                response = await _orchestrationService.ProcessAskRequestAsync(request, cts.Token);
            }

            stopwatch.Stop();

            result.Answer = response.Answer;
            result.Citations = response.Citations;
            result.ResponseTime = stopwatch.Elapsed;
            result.HasCitations = response.Citations.Count > 0;
            result.ContainsExpectedContent = ContainsExpectedContent(response.Answer, goldenItem.MustContain);
            result.CitesExpectedSource = CitesExpectedSource(response.Citations, goldenItem.MustCiteTitle);

            _logger.LogDebug("Evaluated question {QuestionId}: Success={Success}, Citations={Citations}, Time={Time}ms",
                goldenItem.Id, result.IsSuccessful, result.Citations.Count, result.ResponseTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogWarning(ex, "Evaluation failed for question {QuestionId}", goldenItem.Id);
        }

        return result;
    }

    private bool ContainsExpectedContent(string answer, string mustContain)
    {
        if (string.IsNullOrEmpty(mustContain)) return true;
        
        var expectedPhrases = mustContain.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return expectedPhrases.Any(phrase => answer.Contains(phrase.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private bool CitesExpectedSource(List<CitationDto> citations, string mustCiteTitle)
    {
        if (string.IsNullOrEmpty(mustCiteTitle)) return true;
        
        var expectedTitles = mustCiteTitle.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return expectedTitles.Any(title => 
            citations.Any(citation => citation.Title.Contains(title.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private async Task CreateDefaultGoldenDatasetAsync(string filePath, CancellationToken cancellationToken)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Id,Question,MustContain,MustCiteTitle,Category");
        
        var goldenQuestions = new[]
        {
            ("Q001", "What PPE is required for construction work?", "hard hats|safety glasses|steel-toed boots", "Safety Equipment Requirements|PPE", "safety"),
            ("Q002", "How do I report a workplace incident?", "24 hours|safety coordinator|Form WS-101", "Incident Reporting Procedures", "reporting"),
            ("Q003", "What should I do in a fire emergency?", "activate alarm|evacuate|muster point", "Emergency Evacuation Plan", "emergency"),
            ("Q004", "How should chemicals be stored?", "appropriate containers|labeled|trained personnel", "Chemical Handling Protocols", "chemical"),
            ("Q005", "What is required for return to work?", "medical clearance|healthcare provider", "Return to Work Guidelines", "medical"),
            ("Q006", "What forms are needed for incident reporting?", "Form WS-101", "Incident Reporting", "forms"),
            ("Q007", "Who should I contact in an emergency?", "safety coordinator|supervisor|911", "Emergency Procedures", "emergency"),
            ("Q008", "What training is required for chemical handling?", "trained personnel|proper PPE", "Chemical Safety", "training"),
            ("Q009", "How long do I have to report an incident?", "24 hours|immediately", "Incident Reporting", "reporting"),
            ("Q010", "What accommodations are available for return to work?", "gradual return|modified duties|reduced hours", "Return to Work", "medical"),
            ("Q011", "What safety equipment is mandatory?", "hard hats|safety glasses|steel-toed boots", "PPE Requirements", "safety"),
            ("Q012", "Where is the emergency assembly point?", "parking lot|muster point", "Emergency Procedures", "emergency"),
            ("Q013", "What information must be included in incident reports?", "date|time|location|personnel|witnesses", "Incident Documentation", "reporting"),
            ("Q014", "What PPE standards must be met?", "ANSI|standards", "Safety Standards", "compliance"),
            ("Q015", "How are hazardous materials identified?", "labels|SDS|Safety Data Sheets", "Chemical Safety", "hazmat"),
            ("Q016", "What is the evacuation procedure?", "nearest exit|designated routes|assembly area", "Emergency Evacuation", "emergency"),
            ("Q017", "Who can handle hazardous chemicals?", "trained personnel|authorized staff", "Chemical Handling", "training"),
            ("Q018", "What medical documentation is needed for return to work?", "medical clearance|healthcare provider", "Medical Requirements", "medical"),
            ("Q019", "How often are emergency drills conducted?", "quarterly|regular", "Emergency Preparedness", "training"),
            ("Q020", "What should I do if PPE is damaged?", "report|replace|safety coordinator", "Equipment Maintenance", "safety")
        };

        foreach (var (id, question, mustContain, mustCiteTitle, category) in goldenQuestions)
        {
            csv.AppendLine($"{id},\"{question}\",\"{mustContain}\",\"{mustCiteTitle}\",{category}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "./tests");
        await File.WriteAllTextAsync(filePath, csv.ToString(), cancellationToken);

        _logger.LogInformation("Created golden dataset with {Count} questions at {Path}", goldenQuestions.Length, filePath);
    }

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Trim());
        return result.ToArray();
    }

    private string GenerateMarkdownReport(EvaluationReport report)
    {
        var md = new StringBuilder();
        
        md.AppendLine("# OHS Copilot Evaluation Report");
        md.AppendLine();
        md.AppendLine($"**Generated**: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine($"**Report ID**: {report.ReportId}");
        md.AppendLine($"**Mode**: {report.Mode}");
        md.AppendLine();

        if (report.Mode == "Demo" || report.Mode == "Both")
        {
            md.AppendLine("## Demo Mode Results");
            AppendMetricsSection(md, report.DemoModeMetrics, "Demo");
        }

        if (report.LiveModeMetrics != null)
        {
            md.AppendLine("## Live Mode Results");
            AppendMetricsSection(md, report.LiveModeMetrics, "Live");
        }

        md.AppendLine("## Detailed Results");
        md.AppendLine();
        md.AppendLine("| Question ID | Success | Citations | Expected Content | Correct Citation | Response Time |");
        md.AppendLine("|-------------|---------|-----------|------------------|------------------|---------------|");

        foreach (var result in report.Results.OrderBy(r => r.QuestionId))
        {
            var success = result.IsSuccessful ? "✅" : "❌";
            var citations = result.HasCitations ? "✅" : "❌";
            var content = result.ContainsExpectedContent ? "✅" : "❌";
            var correctCitation = result.CitesExpectedSource ? "✅" : "❌";
            var responseTime = $"{result.ResponseTime.TotalMilliseconds:F0}ms";

            md.AppendLine($"| {result.QuestionId} | {success} | {citations} | {content} | {correctCitation} | {responseTime} |");
        }

        if (report.DemoModeMetrics.ErrorsByCategory.Count > 0)
        {
            md.AppendLine();
            md.AppendLine("## Error Analysis");
            md.AppendLine();
            
            foreach (var error in report.DemoModeMetrics.ErrorsByCategory)
            {
                md.AppendLine($"- **{error.Key}**: {error.Value} occurrences");
            }
        }

        md.AppendLine();
        md.AppendLine("## Summary");
        md.AppendLine(report.Summary);

        return md.ToString();
    }

    private void AppendMetricsSection(StringBuilder md, EvaluationMetrics metrics, string mode)
    {
        md.AppendLine();
        md.AppendLine($"### {mode} Mode Metrics");
        md.AppendLine();
        md.AppendLine("| Metric | Value | Target | Status |");
        md.AppendLine("|--------|--------|--------|---------|");
        md.AppendLine($"| Groundedness | {metrics.GroundednessPercentage:F1}% | ≥95% | {(metrics.GroundednessPercentage >= 95 ? "✅ PASS" : "❌ FAIL")} |");
        md.AppendLine($"| Citation Precision | {metrics.CitationPrecisionPercentage:F1}% | ≥80% | {(metrics.CitationPrecisionPercentage >= 80 ? "✅ PASS" : "❌ FAIL")} |");
        md.AppendLine($"| Overall Success | {metrics.OverallSuccessPercentage:F1}% | ≥90% | {(metrics.OverallSuccessPercentage >= 90 ? "✅ PASS" : "❌ FAIL")} |");
        md.AppendLine($"| Average Response Time | {metrics.AverageResponseTime.TotalMilliseconds:F0}ms | ≤500ms | {(metrics.AverageResponseTime.TotalMilliseconds <= 500 ? "✅ PASS" : "❌ FAIL")} |");
        md.AppendLine();
        md.AppendLine($"**Total Questions**: {metrics.TotalQuestions}");
        md.AppendLine($"**Successful Responses**: {metrics.SuccessfulResponses}");
        md.AppendLine($"**Error Rate**: {(metrics.ErrorResponses / (double)metrics.TotalQuestions * 100):F1}%");
    }
}
