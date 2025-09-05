namespace OHS.Copilot.Application.Interfaces;

public interface IDocumentParser
{
    bool CanParse(string filePath);
    Task<DocumentParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default);
    Task<DocumentParseResult> ParseFromStreamAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}

public class DocumentParseResult
{
    public bool Success { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
    public List<DocumentSection> Sections { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public static DocumentParseResult Successful(string title, string content, Dictionary<string, string>? metadata = null, List<DocumentSection>? sections = null)
    {
        return new DocumentParseResult
        {
            Success = true,
            Title = title,
            Content = content,
            Metadata = metadata ?? [],
            Sections = sections ?? []
        };
    }

    public static DocumentParseResult Failed(string errorMessage)
    {
        return new DocumentParseResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

public class DocumentSection
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }

    public static DocumentSection Create(string title, string content, int level = 1, int startPosition = 0, int endPosition = 0)
    {
        return new DocumentSection
        {
            Title = title,
            Content = content,
            Level = level,
            StartPosition = startPosition,
            EndPosition = endPosition
        };
    }
}
