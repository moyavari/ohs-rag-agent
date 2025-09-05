using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;

namespace OHS.Copilot.Infrastructure.Parsers;

public class MarkdownDocumentParser : IDocumentParser
{
    private readonly ILogger<MarkdownDocumentParser> _logger;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownDocumentParser(ILogger<MarkdownDocumentParser> logger)
    {
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".md" or ".markdown";
    }

    public async Task<DocumentParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return DocumentParseResult.Failed($"File not found: {filePath}");
            }

            var markdownContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            return ParseMarkdownContent(markdownContent, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Markdown file: {FilePath}", filePath);
            return DocumentParseResult.Failed($"Markdown parsing failed: {ex.Message}");
        }
    }

    public async Task<DocumentParseResult> ParseFromStreamAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(stream);
            var markdownContent = await reader.ReadToEndAsync(cancellationToken);
            
            return ParseMarkdownContent(markdownContent, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Markdown stream: {FileName}", fileName);
            return DocumentParseResult.Failed($"Markdown parsing failed: {ex.Message}");
        }
    }

    private DocumentParseResult ParseMarkdownContent(string markdownContent, string fileName)
    {
        try
        {
            _logger.LogDebug("Parsing Markdown: {FileName}", fileName);

            var document = Markdown.Parse(markdownContent, _pipeline);
            
            var title = ExtractTitle(document, fileName);
            var sections = ExtractSections(document);
            var content = ExtractPlainText(document);
            var metadata = ExtractMetadata(document);

            _logger.LogDebug("Successfully parsed Markdown: {FileName}, Sections: {SectionCount}, Content Length: {ContentLength}",
                fileName, sections.Count, content.Length);

            return DocumentParseResult.Successful(title, content, metadata, sections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Markdown content: {FileName}", fileName);
            return DocumentParseResult.Failed($"Markdown parsing failed: {ex.Message}");
        }
    }

    private string ExtractTitle(MarkdownDocument document, string fallbackTitle)
    {
        var firstHeading = document.Descendants<HeadingBlock>().FirstOrDefault();
        
        if (firstHeading != null)
        {
            var title = ExtractTextFromInlines(firstHeading.Inline);
            if (!string.IsNullOrEmpty(title))
            {
                return title.Trim();
            }
        }

        var lines = document.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.FirstOrDefault()?.Trim();
        
        if (!string.IsNullOrEmpty(firstLine) && firstLine.StartsWith("#"))
        {
            return firstLine.TrimStart('#').Trim();
        }

        return Path.GetFileNameWithoutExtension(fallbackTitle);
    }

    private List<DocumentSection> ExtractSections(MarkdownDocument document)
    {
        var sections = new List<DocumentSection>();

        try
        {
            var headings = document.Descendants<HeadingBlock>().ToList();
            
            for (int i = 0; i < headings.Count; i++)
            {
                var heading = headings[i];
                var title = ExtractTextFromInlines(heading.Inline);
                
                var sectionContent = ExtractSectionContent(document, heading, 
                    i < headings.Count - 1 ? headings[i + 1] : null);
                
                if (!string.IsNullOrEmpty(sectionContent))
                {
                    sections.Add(DocumentSection.Create(
                        title: title,
                        content: sectionContent,
                        level: heading.Level,
                        startPosition: heading.Line,
                        endPosition: heading.Line + sectionContent.Split('\n').Length
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract Markdown sections");
        }

        return sections;
    }

    private string ExtractSectionContent(MarkdownDocument document, HeadingBlock currentHeading, HeadingBlock? nextHeading)
    {
        var content = new List<string>();
        var currentLine = currentHeading.Line + 1;
        var endLine = nextHeading?.Line ?? int.MaxValue;

        var blocks = document.Where(block => block.Line >= currentLine && block.Line < endLine);
        
        foreach (var block in blocks)
        {
            if (block is not HeadingBlock)
            {
                var blockText = ExtractTextFromBlock(block);
                if (!string.IsNullOrWhiteSpace(blockText))
                {
                    content.Add(blockText);
                }
            }
        }

        return string.Join("\n\n", content);
    }

    private string ExtractPlainText(MarkdownDocument document)
    {
        var content = new List<string>();

        foreach (var block in document)
        {
            var blockText = ExtractTextFromBlock(block);
            if (!string.IsNullOrWhiteSpace(blockText))
            {
                content.Add(blockText);
            }
        }

        return string.Join("\n\n", content);
    }

    private string ExtractTextFromBlock(Block block)
    {
        return block switch
        {
            ParagraphBlock paragraph => ExtractTextFromInlines(paragraph.Inline),
            HeadingBlock heading => ExtractTextFromInlines(heading.Inline),
            QuoteBlock quote => string.Join("\n", quote.Select(ExtractTextFromBlock)),
            ListBlock list => ExtractListText(list),
            CodeBlock code => code.Lines.ToString(),
            _ => block.ToString() ?? ""
        };
    }

    private string ExtractListText(ListBlock listBlock)
    {
        var items = new List<string>();

        foreach (var listItem in listBlock.OfType<ListItemBlock>())
        {
            var itemText = string.Join(" ", listItem.Select(ExtractTextFromBlock));
            if (!string.IsNullOrWhiteSpace(itemText))
            {
                items.Add($"• {itemText}");
            }
        }

        return string.Join("\n", items);
    }

    private string ExtractTextFromInlines(ContainerInline? inlines)
    {
        if (inlines == null) return string.Empty;

        var text = new List<string>();

        foreach (var inline in inlines)
        {
            var inlineText = inline switch
            {
                LiteralInline literal => literal.Content.ToString(),
                LinkInline link => ExtractTextFromInlines(link),
                EmphasisInline emphasis => ExtractTextFromInlines(emphasis),
                CodeInline code => code.Content,
                LineBreakInline => "\n",
                _ => inline.ToString() ?? ""
            };

            if (!string.IsNullOrEmpty(inlineText))
            {
                text.Add(inlineText);
            }
        }

        return string.Join("", text);
    }

    private Dictionary<string, string> ExtractMetadata(MarkdownDocument document)
    {
        var metadata = new Dictionary<string, string>
        {
            ["FileType"] = "Markdown"
        };

        try
        {
            var headingCount = document.Descendants<HeadingBlock>().Count();
            var paragraphCount = document.Descendants<ParagraphBlock>().Count();
            var listCount = document.Descendants<ListBlock>().Count();

            metadata["HeadingCount"] = headingCount.ToString();
            metadata["ParagraphCount"] = paragraphCount.ToString();
            metadata["ListCount"] = listCount.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract Markdown metadata");
        }

        return metadata;
    }
}
