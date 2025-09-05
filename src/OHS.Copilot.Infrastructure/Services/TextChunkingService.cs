using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Domain.Entities;
using System.Text.RegularExpressions;

namespace OHS.Copilot.Infrastructure.Services;

public class TextChunkingService : ITextChunkingService
{
    private readonly ILogger<TextChunkingService> _logger;

    public TextChunkingService(ILogger<TextChunkingService> logger)
    {
        _logger = logger;
    }

    public List<Chunk> ChunkDocument(DocumentParseResult document, ChunkingOptions options)
    {
        if (!document.Success || string.IsNullOrEmpty(document.Content))
        {
            return [];
        }

        _logger.LogDebug("Chunking document: {Title} with strategy {Strategy}, chunk size {ChunkSize}", 
            document.Title, options.Strategy, options.ChunkSize);

        var chunks = new List<Chunk>();

        if (document.Sections.Count > 0 && options.PreserveContext)
        {
            foreach (var section in document.Sections)
            {
                var sectionChunks = ChunkText(section.Content, options);
                
                foreach (var chunkText in sectionChunks)
                {
                    var chunk = Chunk.Create(chunkText, document.Title, section.Title, "document");
                    
                    foreach (var metadata in document.Metadata)
                    {
                        chunk.Metadata[metadata.Key] = metadata.Value;
                    }
                    chunk.Metadata["SectionLevel"] = section.Level.ToString();
                    
                    chunks.Add(chunk);
                }
            }
        }
        else
        {
            var textChunks = ChunkText(document.Content, options);
            
            foreach (var chunkText in textChunks)
            {
                var chunk = Chunk.Create(chunkText, document.Title, "Content", "document");
                
                foreach (var metadata in document.Metadata)
                {
                    chunk.Metadata[metadata.Key] = metadata.Value;
                }
                
                chunks.Add(chunk);
            }
        }

        _logger.LogDebug("Created {ChunkCount} chunks from document: {Title}", chunks.Count, document.Title);

        return chunks;
    }

    public List<string> ChunkText(string text, ChunkingOptions options)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return options.Strategy switch
        {
            ChunkingStrategy.Recursive => ChunkTextRecursive(text, options),
            ChunkingStrategy.Semantic => ChunkTextSemantic(text, options),
            ChunkingStrategy.Sentence => ChunkTextBySentences(text, options),
            ChunkingStrategy.Paragraph => ChunkTextByParagraphs(text, options),
            _ => ChunkTextRecursive(text, options)
        };
    }

    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (int)Math.Ceiling(words.Length * 1.3);
    }

    private List<string> ChunkTextRecursive(string text, ChunkingOptions options)
    {
        var chunks = new List<string>();
        
        if (text.Length <= options.ChunkSize)
        {
            return [text];
        }

        foreach (var separator in options.SeparatorHierarchy)
        {
            if (text.Contains(separator))
            {
                var parts = text.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
                var currentChunk = new List<string>();
                var currentLength = 0;

                foreach (var part in parts)
                {
                    var partLength = part.Length + separator.Length;

                    if (currentLength + partLength > options.ChunkSize && currentChunk.Count > 0)
                    {
                        var chunkText = string.Join(separator, currentChunk);
                        if (chunkText.Length >= options.MinChunkSize)
                        {
                            chunks.Add(chunkText);
                        }

                        if (options.ChunkOverlap > 0 && currentChunk.Count > 1)
                        {
                            var overlapCount = Math.Min(currentChunk.Count - 1, 
                                (int)Math.Ceiling(options.ChunkOverlap / (double)options.ChunkSize * currentChunk.Count));
                            currentChunk = currentChunk.TakeLast(overlapCount).ToList();
                            currentLength = currentChunk.Sum(c => c.Length + separator.Length);
                        }
                        else
                        {
                            currentChunk.Clear();
                            currentLength = 0;
                        }
                    }

                    currentChunk.Add(part);
                    currentLength += partLength;
                }

                if (currentChunk.Count > 0)
                {
                    var chunkText = string.Join(separator, currentChunk);
                    if (chunkText.Length >= options.MinChunkSize)
                    {
                        chunks.Add(chunkText);
                    }
                }

                return chunks;
            }
        }

        return ChunkTextByCharacterLength(text, options);
    }

    private List<string> ChunkTextSemantic(string text, ChunkingOptions options)
    {
        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentLength = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphLength = paragraph.Length;

            if (currentLength + paragraphLength > options.ChunkSize && currentChunk.Count > 0)
            {
                var chunkText = string.Join("\n\n", currentChunk);
                if (chunkText.Length >= options.MinChunkSize)
                {
                    chunks.Add(chunkText);
                }

                if (options.ChunkOverlap > 0 && currentChunk.Count > 1)
                {
                    var overlapCount = Math.Min(currentChunk.Count - 1, 
                        (int)Math.Ceiling(options.ChunkOverlap / (double)options.ChunkSize * currentChunk.Count));
                    currentChunk = currentChunk.TakeLast(overlapCount).ToList();
                    currentLength = currentChunk.Sum(c => c.Length + 2);
                }
                else
                {
                    currentChunk.Clear();
                    currentLength = 0;
                }
            }

            currentChunk.Add(paragraph);
            currentLength += paragraphLength + 2;
        }

        if (currentChunk.Count > 0)
        {
            var chunkText = string.Join("\n\n", currentChunk);
            if (chunkText.Length >= options.MinChunkSize)
            {
                chunks.Add(chunkText);
            }
        }

        return chunks;
    }

    private List<string> ChunkTextBySentences(string text, ChunkingOptions options)
    {
        var sentences = SplitIntoSentences(text);
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            var sentenceLength = sentence.Length;

            if (currentLength + sentenceLength > options.ChunkSize && currentChunk.Count > 0)
            {
                var chunkText = string.Join(" ", currentChunk);
                if (chunkText.Length >= options.MinChunkSize)
                {
                    chunks.Add(chunkText);
                }

                if (options.ChunkOverlap > 0 && currentChunk.Count > 1)
                {
                    var overlapCount = Math.Min(currentChunk.Count - 1, 
                        (int)Math.Ceiling(options.ChunkOverlap / (double)options.ChunkSize * currentChunk.Count));
                    currentChunk = currentChunk.TakeLast(overlapCount).ToList();
                    currentLength = currentChunk.Sum(c => c.Length + 1);
                }
                else
                {
                    currentChunk.Clear();
                    currentLength = 0;
                }
            }

            currentChunk.Add(sentence);
            currentLength += sentenceLength + 1;
        }

        if (currentChunk.Count > 0)
        {
            var chunkText = string.Join(" ", currentChunk);
            if (chunkText.Length >= options.MinChunkSize)
            {
                chunks.Add(chunkText);
            }
        }

        return chunks;
    }

    private List<string> ChunkTextByParagraphs(string text, ChunkingOptions options)
    {
        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        return paragraphs;
    }

    private List<string> ChunkTextByCharacterLength(string text, ChunkingOptions options)
    {
        var chunks = new List<string>();

        for (int i = 0; i < text.Length; i += options.ChunkSize - options.ChunkOverlap)
        {
            var chunkLength = Math.Min(options.ChunkSize, text.Length - i);
            var chunk = text.Substring(i, chunkLength);

            if (chunk.Length >= options.MinChunkSize)
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    private List<string> SplitIntoSentences(string text)
    {
        var sentencePattern = @"(?<=[.!?])\s+(?=[A-Z])";
        var sentences = Regex.Split(text, sentencePattern)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return sentences;
    }
}
