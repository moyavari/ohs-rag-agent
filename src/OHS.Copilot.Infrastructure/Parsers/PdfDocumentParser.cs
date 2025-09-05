using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace OHS.Copilot.Infrastructure.Parsers;

public class PdfDocumentParser : IDocumentParser
{
    private readonly ILogger<PdfDocumentParser> _logger;

    public PdfDocumentParser(ILogger<PdfDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension == ".pdf";
    }

    public async Task<DocumentParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return DocumentParseResult.Failed($"File not found: {filePath}");
            }

            using var stream = File.OpenRead(filePath);
            return await ParseFromStreamAsync(stream, Path.GetFileName(filePath), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PDF file: {FilePath}", filePath);
            return DocumentParseResult.Failed($"PDF parsing failed: {ex.Message}");
        }
    }

    public Task<DocumentParseResult> ParseFromStreamAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Parsing PDF: {FileName}", fileName);

            using var document = PdfDocument.Open(stream);
            
            var title = ExtractTitle(document, fileName);
            var content = new List<string>();
            var sections = new List<DocumentSection>();
            var metadata = ExtractMetadata(document);

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var pageText = ExtractPageText(page);
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    content.Add(pageText);
                    
                    var section = DocumentSection.Create(
                        title: $"Page {page.Number}",
                        content: pageText,
                        level: 1,
                        startPosition: content.Count - 1,
                        endPosition: content.Count - 1
                    );
                    
                    sections.Add(section);
                }
            }

            var fullContent = string.Join("\n\n", content);
            
            _logger.LogDebug("Successfully parsed PDF: {FileName}, Pages: {PageCount}, Content Length: {ContentLength}",
                fileName, document.NumberOfPages, fullContent.Length);

            return Task.FromResult(DocumentParseResult.Successful(title, fullContent, metadata, sections));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PDF stream: {FileName}", fileName);
            return Task.FromResult(DocumentParseResult.Failed($"PDF parsing failed: {ex.Message}"));
        }
    }

    private string ExtractTitle(PdfDocument document, string fallbackTitle)
    {
        try
        {
            var title = document.Information?.Title;
            
            if (!string.IsNullOrEmpty(title))
            {
                return title.Trim();
            }

            if (document.GetPages().Any())
            {
                var firstPage = document.GetPage(1);
                var firstPageText = ExtractPageText(firstPage);
                var firstLine = firstPageText.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                
                if (!string.IsNullOrEmpty(firstLine) && firstLine.Length < 100)
                {
                    return firstLine.Trim();
                }
            }

            return Path.GetFileNameWithoutExtension(fallbackTitle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract PDF title, using fallback");
            return Path.GetFileNameWithoutExtension(fallbackTitle);
        }
    }

    private Dictionary<string, string> ExtractMetadata(PdfDocument document)
    {
        var metadata = new Dictionary<string, string>();

        try
        {
            if (document.Information != null)
            {
                var info = document.Information;
                
                if (!string.IsNullOrEmpty(info.Author))
                    metadata["Author"] = info.Author;
                
                if (!string.IsNullOrEmpty(info.Subject))
                    metadata["Subject"] = info.Subject;
                
                if (!string.IsNullOrEmpty(info.Creator))
                    metadata["Creator"] = info.Creator;
                
                try
                {
                    if (!string.IsNullOrEmpty(info.CreationDate))
                        metadata["CreationDate"] = info.CreationDate;
                }
                catch (Exception)
                {
                    // Ignore date parsing errors
                }
            }

            metadata["PageCount"] = document.NumberOfPages.ToString();
            metadata["FileType"] = "PDF";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract PDF metadata");
        }

        return metadata;
    }

    private string ExtractPageText(Page page)
    {
        try
        {
            var words = page.GetWords();
            var lines = new List<string>();
            var currentLine = new List<string>();
            double? lastY = null;

            foreach (var word in words.OrderBy(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
            {
                var currentY = Math.Round(word.BoundingBox.Bottom, 1);
                
                if (lastY.HasValue && Math.Abs(currentY - lastY.Value) > 2)
                {
                    if (currentLine.Count > 0)
                    {
                        lines.Add(string.Join(" ", currentLine));
                        currentLine.Clear();
                    }
                }

                currentLine.Add(word.Text);
                lastY = currentY;
            }

            if (currentLine.Count > 0)
            {
                lines.Add(string.Join(" ", currentLine));
            }

            return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text from PDF page {PageNumber}", page.Number);
            return string.Empty;
        }
    }
}
