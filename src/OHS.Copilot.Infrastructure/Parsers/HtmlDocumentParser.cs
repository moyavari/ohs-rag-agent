using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;

namespace OHS.Copilot.Infrastructure.Parsers;

public class HtmlDocumentParser : IDocumentParser
{
    private readonly ILogger<HtmlDocumentParser> _logger;

    public HtmlDocumentParser(ILogger<HtmlDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".html" or ".htm";
    }

    public async Task<DocumentParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return DocumentParseResult.Failed($"File not found: {filePath}");
            }

            var htmlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            return ParseHtmlContent(htmlContent, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse HTML file: {FilePath}", filePath);
            return DocumentParseResult.Failed($"HTML parsing failed: {ex.Message}");
        }
    }

    public async Task<DocumentParseResult> ParseFromStreamAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(stream);
            var htmlContent = await reader.ReadToEndAsync(cancellationToken);
            
            return ParseHtmlContent(htmlContent, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse HTML stream: {FileName}", fileName);
            return DocumentParseResult.Failed($"HTML parsing failed: {ex.Message}");
        }
    }

    private DocumentParseResult ParseHtmlContent(string htmlContent, string fileName)
    {
        try
        {
            _logger.LogDebug("Parsing HTML: {FileName}", fileName);

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var title = ExtractTitle(doc, fileName);
            var sections = ExtractSections(doc);
            var content = ExtractTextContent(doc);
            var metadata = ExtractMetadata(doc);

            _logger.LogDebug("Successfully parsed HTML: {FileName}, Sections: {SectionCount}, Content Length: {ContentLength}",
                fileName, sections.Count, content.Length);

            return DocumentParseResult.Successful(title, content, metadata, sections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse HTML content: {FileName}", fileName);
            return DocumentParseResult.Failed($"HTML parsing failed: {ex.Message}");
        }
    }

    private string ExtractTitle(HtmlDocument doc, string fallbackTitle)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        if (titleNode != null && !string.IsNullOrWhiteSpace(titleNode.InnerText))
        {
            return HtmlEntity.DeEntitize(titleNode.InnerText).Trim();
        }

        var h1Node = doc.DocumentNode.SelectSingleNode("//h1");
        if (h1Node != null && !string.IsNullOrWhiteSpace(h1Node.InnerText))
        {
            return HtmlEntity.DeEntitize(h1Node.InnerText).Trim();
        }

        return Path.GetFileNameWithoutExtension(fallbackTitle);
    }

    private List<DocumentSection> ExtractSections(HtmlDocument doc)
    {
        var sections = new List<DocumentSection>();

        try
        {
            var headings = doc.DocumentNode.SelectNodes("//h1 | //h2 | //h3 | //h4 | //h5 | //h6");
            
            if (headings != null)
            {
                foreach (var heading in headings)
                {
                    var level = int.Parse(heading.Name.Substring(1));
                    var title = HtmlEntity.DeEntitize(heading.InnerText).Trim();
                    
                    var content = ExtractSectionContent(heading);
                    
                    if (!string.IsNullOrEmpty(content))
                    {
                        sections.Add(DocumentSection.Create(title, content, level));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract HTML sections");
        }

        return sections;
    }

    private string ExtractSectionContent(HtmlNode headingNode)
    {
        var content = new List<string>();
        var currentNode = headingNode.NextSibling;

        while (currentNode != null)
        {
            if (currentNode.Name.StartsWith("h") && currentNode.Name.Length == 2 && char.IsDigit(currentNode.Name[1]))
            {
                break;
            }

            if (currentNode.NodeType == HtmlNodeType.Element)
            {
                var nodeText = HtmlEntity.DeEntitize(currentNode.InnerText).Trim();
                if (!string.IsNullOrEmpty(nodeText))
                {
                    content.Add(nodeText);
                }
            }

            currentNode = currentNode.NextSibling;
        }

        return string.Join("\n", content);
    }

    private string ExtractTextContent(HtmlDocument doc)
    {
        var bodyNode = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        
        var textContent = bodyNode.SelectNodes("//text()[normalize-space()]");
        
        if (textContent == null)
        {
            return HtmlEntity.DeEntitize(bodyNode.InnerText).Trim();
        }

        var contentParts = textContent
            .Select(node => HtmlEntity.DeEntitize(node.InnerText).Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        return string.Join(" ", contentParts);
    }

    private Dictionary<string, string> ExtractMetadata(HtmlDocument doc)
    {
        var metadata = new Dictionary<string, string>
        {
            ["FileType"] = "HTML"
        };

        try
        {
            var metaTags = doc.DocumentNode.SelectNodes("//meta[@name or @property]");
            
            if (metaTags != null)
            {
                foreach (var metaTag in metaTags)
                {
                    var name = metaTag.GetAttributeValue("name", "") ?? metaTag.GetAttributeValue("property", "");
                    var content = metaTag.GetAttributeValue("content", "");
                    
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(content))
                    {
                        metadata[name] = content;
                    }
                }
            }

            var descriptionMeta = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            if (descriptionMeta != null)
            {
                metadata["Description"] = descriptionMeta.GetAttributeValue("content", "");
            }

            var keywordsMeta = doc.DocumentNode.SelectSingleNode("//meta[@name='keywords']");
            if (keywordsMeta != null)
            {
                metadata["Keywords"] = keywordsMeta.GetAttributeValue("content", "");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract HTML metadata");
        }

        return metadata;
    }
}
