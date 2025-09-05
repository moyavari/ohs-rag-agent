using System.ComponentModel.DataAnnotations;

namespace OHS.Copilot.Application.DTOs.Requests;

public class IngestRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string DirectoryOrZipPath { get; set; } = string.Empty;

    public int ChunkSize { get; set; } = 1000;
    public int ChunkOverlap { get; set; } = 200;
    public bool RebuildIndex { get; set; } = false;
    public List<string> SupportedExtensions { get; set; } = [".pdf", ".html", ".md", ".txt"];
}
