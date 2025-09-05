using System.ComponentModel.DataAnnotations;

namespace OHS.Copilot.Application.DTOs.Requests;

public class AskRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Question { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ConversationId { get; set; }

    public int MaxTokens { get; set; } = 2000;
    public int TopK { get; set; } = 10;
    public bool EnableRerank { get; set; } = false;
}
