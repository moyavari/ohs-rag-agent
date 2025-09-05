using OHS.Copilot.Domain.ValueObjects;

namespace OHS.Copilot.Application.DTOs.Responses;

public class AskResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<CitationDto> Citations { get; set; } = [];
    public string? ConversationId { get; set; }
    public ResponseMetadata Metadata { get; set; } = new();

    public static AskResponse FromAnswer(Answer answer, string? conversationId = null)
    {
        return new AskResponse
        {
            Answer = answer.Content,
            Citations = answer.Citations.Select(CitationDto.FromCitation).ToList(),
            ConversationId = conversationId
        };
    }
}

public class CitationDto
{
    public string Id { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string Text { get; set; } = string.Empty;

    public static CitationDto FromCitation(Citation citation)
    {
        return new CitationDto
        {
            Id = citation.Id,
            Score = citation.Score,
            Title = citation.Title,
            Url = citation.Url,
            Text = citation.Text
        };
    }
}
