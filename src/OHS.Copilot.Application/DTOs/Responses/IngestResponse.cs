namespace OHS.Copilot.Application.DTOs.Responses;

public class IngestResponse
{
    public int ProcessedFiles { get; set; }
    public int GeneratedChunks { get; set; }
    public int UniqueHashes { get; set; }
    public int SkippedDuplicates { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public List<string> ErrorMessages { get; set; } = [];
    public List<ProcessedFileInfo> FileDetails { get; set; } = [];

    public bool HasErrors()
    {
        return ErrorMessages.Count > 0;
    }
}

public class ProcessedFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
