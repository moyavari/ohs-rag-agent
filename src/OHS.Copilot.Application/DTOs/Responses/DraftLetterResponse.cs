using OHS.Copilot.Domain.ValueObjects;

namespace OHS.Copilot.Application.DTOs.Responses;

public class DraftLetterResponse
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<string> Placeholders { get; set; } = [];
    public string? ConversationId { get; set; }
    public ResponseMetadata Metadata { get; set; } = new();

    public static DraftLetterResponse FromLetterDraft(LetterDraft draft, string? conversationId = null)
    {
        return new DraftLetterResponse
        {
            Subject = draft.Subject,
            Body = draft.Body,
            Placeholders = draft.Placeholders.ToList(),
            ConversationId = conversationId
        };
    }
}
