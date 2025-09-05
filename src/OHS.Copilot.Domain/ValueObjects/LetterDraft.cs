namespace OHS.Copilot.Domain.ValueObjects;

public record LetterDraft
{
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public IReadOnlyList<string> Placeholders { get; init; } = [];

    public static LetterDraft Create(string subject, string body, IEnumerable<string> placeholders)
    {
        return new LetterDraft
        {
            Subject = subject,
            Body = body,
            Placeholders = placeholders.ToList()
        };
    }

    public bool HasPlaceholders()
    {
        return Placeholders.Count > 0;
    }

    public int WordCount()
    {
        return Body.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
