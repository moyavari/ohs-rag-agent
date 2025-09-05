using OHS.Copilot.Application.DTOs.Responses;

namespace OHS.Copilot.Application.Interfaces;

public interface IEvaluationService
{
    Task<EvaluationReport> RunEvaluationAsync(EvaluationOptions? options = null, CancellationToken cancellationToken = default);
    Task<List<GoldenDataItem>> LoadGoldenDatasetAsync(CancellationToken cancellationToken = default);
    Task<EvaluationMetrics> CalculateMetricsAsync(List<EvaluationResult> results, CancellationToken cancellationToken = default);
    Task SaveEvaluationReportAsync(EvaluationReport report, string filePath, CancellationToken cancellationToken = default);
}

public class EvaluationOptions
{
    public string GoldenDatasetPath { get; set; } = "./tests/golden.csv";
    public string ReportOutputPath { get; set; } = "./EVAL_REPORT.md";
    public bool RunInDemoMode { get; set; } = false;
    public bool RunInLiveMode { get; set; } = false;
    public int MaxConcurrentRequests { get; set; } = 5;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public static EvaluationOptions Default()
    {
        return new EvaluationOptions();
    }
}

public class GoldenDataItem
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string MustContain { get; set; } = string.Empty;
    public string MustCiteTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double ExpectedConfidence { get; set; } = 0.8;
    public List<string> Keywords { get; set; } = [];

    public static GoldenDataItem Create(string id, string question, string mustContain, string mustCiteTitle, string category = "general")
    {
        return new GoldenDataItem
        {
            Id = id,
            Question = question,
            MustContain = mustContain,
            MustCiteTitle = mustCiteTitle,
            Category = category
        };
    }
}

public class EvaluationResult
{
    public string QuestionId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<CitationDto> Citations { get; set; } = [];
    public bool HasCitations { get; set; }
    public bool ContainsExpectedContent { get; set; }
    public bool CitesExpectedSource { get; set; }
    public bool RefusedCorrectly { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsSuccessful => HasCitations && ContainsExpectedContent && CitesExpectedSource && string.IsNullOrEmpty(ErrorMessage);
}

public class EvaluationMetrics
{
    public int TotalQuestions { get; set; }
    public int SuccessfulResponses { get; set; }
    public int ResponsesWithCitations { get; set; }
    public int ResponsesWithExpectedContent { get; set; }
    public int ResponsesWithCorrectCitations { get; set; }
    public int ErrorResponses { get; set; }
    
    public double GroundednessPercentage => TotalQuestions > 0 ? (double)ResponsesWithCitations / TotalQuestions * 100 : 0;
    public double CitationPrecisionPercentage => TotalQuestions > 0 ? (double)ResponsesWithCorrectCitations / TotalQuestions * 100 : 0;
    public double OverallSuccessPercentage => TotalQuestions > 0 ? (double)SuccessfulResponses / TotalQuestions * 100 : 0;
    
    public TimeSpan AverageResponseTime { get; set; }
    public Dictionary<string, int> ErrorsByCategory { get; set; } = [];
    public Dictionary<string, double> SuccessRateByCategory { get; set; } = [];
}

public class EvaluationReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Mode { get; set; } = string.Empty; // Demo or Live
    public EvaluationMetrics DemoModeMetrics { get; set; } = new();
    public EvaluationMetrics? LiveModeMetrics { get; set; }
    public List<EvaluationResult> Results { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public Dictionary<string, object> SystemInfo { get; set; } = [];

    public static EvaluationReport Create(string mode, EvaluationMetrics metrics, List<EvaluationResult> results)
    {
        var report = new EvaluationReport
        {
            Mode = mode,
            Results = results
        };

        if (mode == "Demo")
        {
            report.DemoModeMetrics = metrics;
        }
        else
        {
            report.LiveModeMetrics = metrics;
        }

        report.GenerateSummary();
        return report;
    }

    public void GenerateSummary()
    {
        var metrics = Mode == "Demo" ? DemoModeMetrics : LiveModeMetrics ?? new EvaluationMetrics();
        
        Summary = $"""
        ## Evaluation Summary

        **Mode**: {Mode}
        **Generated**: {GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC
        **Total Questions**: {metrics.TotalQuestions}

        ### Key Metrics
        - **Groundedness**: {metrics.GroundednessPercentage:F1}% (≥95% target)
        - **Citation Precision**: {metrics.CitationPrecisionPercentage:F1}% (≥80% target)
        - **Overall Success**: {metrics.OverallSuccessPercentage:F1}%
        - **Average Response Time**: {metrics.AverageResponseTime.TotalMilliseconds:F0}ms

        ### Results
        - ✅ Successful: {metrics.SuccessfulResponses}
        - ❌ Failed: {metrics.ErrorResponses}
        - 📝 With Citations: {metrics.ResponsesWithCitations}
        - 🎯 Expected Content: {metrics.ResponsesWithExpectedContent}
        """;
    }
}
