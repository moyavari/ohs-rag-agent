using OHS.Copilot.Domain.Entities;

namespace OHS.Copilot.Application.Interfaces;

public interface ITextChunkingService
{
    List<Chunk> ChunkDocument(DocumentParseResult document, ChunkingOptions options);
    List<string> ChunkText(string text, ChunkingOptions options);
    int EstimateTokenCount(string text);
}

public class ChunkingOptions
{
    public int ChunkSize { get; set; } = 1000;
    public int ChunkOverlap { get; set; } = 200;
    public ChunkingStrategy Strategy { get; set; } = ChunkingStrategy.Recursive;
    public List<string> SeparatorHierarchy { get; set; } = ["\n\n", "\n", ". ", " "];
    public bool PreserveContext { get; set; } = true;
    public int MinChunkSize { get; set; } = 100;
    public int MaxChunkSize { get; set; } = 2000;

    public static ChunkingOptions Default()
    {
        return new ChunkingOptions();
    }

    public static ChunkingOptions ForLargeDocuments()
    {
        return new ChunkingOptions
        {
            ChunkSize = 1500,
            ChunkOverlap = 300,
            Strategy = ChunkingStrategy.Recursive
        };
    }

    public static ChunkingOptions ForDetailedAnalysis()
    {
        return new ChunkingOptions
        {
            ChunkSize = 500,
            ChunkOverlap = 100,
            Strategy = ChunkingStrategy.Semantic
        };
    }
}

public enum ChunkingStrategy
{
    Recursive,
    Semantic,
    Sentence,
    Paragraph
}
