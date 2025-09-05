using System.ComponentModel.DataAnnotations;

namespace OHS.Copilot.Application.DTOs.Requests;

public class DraftLetterRequest
{
    [StringLength(100)]
    public string? CaseId { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Purpose { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<string> Points { get; set; } = [];

    [StringLength(100)]
    public string? ConversationId { get; set; }

    public int MaxTokens { get; set; } = 2000;
}
