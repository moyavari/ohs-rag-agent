using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;

namespace OHS.Copilot.Infrastructure.Parsers;

public class TextDocumentParser : IDocumentParser
{
    private readonly ILogger<TextDocumentParser> _logger;

    public TextDocumentParser(ILogger<TextDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".txt" or ".text" or "";
    }

    public async Task<DocumentParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return DocumentParseResult.Failed($"File not found: {filePath}");
            }

            var textContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            return ParseTextContent(textContent, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse text file: {FilePath}", filePath);
            return DocumentParseResult.Failed($"Text parsing failed: {ex.Message}");
        }
    }

    public async Task<DocumentParseResult> ParseFromStreamAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(stream);
            var textContent = await reader.ReadToEndAsync(cancellationToken);
            
            return ParseTextContent(textContent, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse text stream: {FileName}", fileName);
            return DocumentParseResult.Failed($"Text parsing failed: {ex.Message}");
        }
    }

    private DocumentParseResult ParseTextContent(string textContent, string fileName)
    {
        try
        {
            _logger.LogDebug("Parsing text: {FileName}", fileName);

            var title = ExtractTitle(textContent, fileName);
            var sections = ExtractSections(textContent);
            var cleanContent = CleanTextContent(textContent);
            var metadata = ExtractMetadata(textContent);

            _logger.LogDebug("Successfully parsed text: {FileName}, Sections: {SectionCount}, Content Length: {ContentLength}",
                fileName, sections.Count, cleanContent.Length);

            return DocumentParseResult.Successful(title, cleanContent, metadata, sections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse text content: {FileName}", fileName);
            return DocumentParseResult.Failed($"Text parsing failed: {ex.Message}");
        }
    }

    private string ExtractTitle(string content, string fallbackTitle)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        var firstLine = lines.FirstOrDefault()?.Trim();
        if (!string.IsNullOrEmpty(firstLine) && firstLine.Length < 100)
        {
            if (firstLine.Contains("title", StringComparison.OrdinalIgnoreCase) ||
                firstLine.All(c => char.IsUpper(c) || char.IsWhiteSpace(c)) ||
                firstLine.EndsWith(':'))
            {
                return firstLine.TrimEnd(':').Trim();
            }
        }

        return Path.GetFileNameWithoutExtension(fallbackTitle);
    }

    private List<DocumentSection> ExtractSections(string content)
    {
        var sections = new List<DocumentSection>();

        try
        {
            var lines = content.Split('\n');
            var currentSection = new List<string>();
            var sectionTitle = "Introduction";
            var sectionLevel = 1;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (IsSectionHeader(line))
                {
                    if (currentSection.Count > 0)
                    {
                        var sectionContent = string.Join("\n", currentSection).Trim();
                        if (!string.IsNullOrEmpty(sectionContent))
                        {
                            sections.Add(DocumentSection.Create(
                                title: sectionTitle,
                                content: sectionContent,
                                level: sectionLevel,
                                startPosition: i - currentSection.Count,
                                endPosition: i
                            ));
                        }
                        currentSection.Clear();
                    }

                    sectionTitle = ExtractSectionTitle(line);
                    sectionLevel = DetermineSectionLevel(line);
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    currentSection.Add(line);
                }
            }

            if (currentSection.Count > 0)
            {
                var sectionContent = string.Join("\n", currentSection).Trim();
                if (!string.IsNullOrEmpty(sectionContent))
                {
                    sections.Add(DocumentSection.Create(
                        title: sectionTitle,
                        content: sectionContent,
                        level: sectionLevel,
                        startPosition: lines.Length - currentSection.Count,
                        endPosition: lines.Length
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text sections");
        }

        return sections;
    }

    private bool IsSectionHeader(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;

        return line.StartsWith("Chapter ", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Section ", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Part ", StringComparison.OrdinalIgnoreCase) ||
               line.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsDigit(c)) && line.Length < 80 ||
               line.EndsWith(':') && line.Length < 80;
    }

    private string ExtractSectionTitle(string line)
    {
        return line.TrimEnd(':').Trim();
    }

    private int DetermineSectionLevel(string line)
    {
        if (line.StartsWith("Chapter ", StringComparison.OrdinalIgnoreCase)) return 1;
        if (line.StartsWith("Section ", StringComparison.OrdinalIgnoreCase)) return 2;
        if (line.StartsWith("Part ", StringComparison.OrdinalIgnoreCase)) return 3;
        if (line.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsDigit(c))) return 1;
        
        return 2;
    }

    private string CleanTextContent(string content)
    {
        var lines = content.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToList();

        return string.Join("\n", lines);
    }

    private Dictionary<string, string> ExtractMetadata(string content)
    {
        var metadata = new Dictionary<string, string>
        {
            ["FileType"] = "Text"
        };

        try
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var paragraphs = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

            metadata["LineCount"] = lines.Length.ToString();
            metadata["WordCount"] = words.Length.ToString();
            metadata["ParagraphCount"] = paragraphs.Length.ToString();
            metadata["CharacterCount"] = content.Length.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text metadata");
        }

        return metadata;
    }
}
