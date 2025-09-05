namespace OHS.Copilot.Domain.ValueObjects;

public record Answer
{
    public string Content { get; init; } = string.Empty;
    public IReadOnlyList<Citation> Citations { get; init; } = [];

    public static Answer Create(string content, IEnumerable<Citation> citations)
    {
        return new Answer
        {
            Content = content,
            Citations = citations.ToList()
        };
    }

    public bool HasCitations()
    {
        return Citations.Count > 0;
    }

    public Citation? GetCitationByIndex(int index)
    {
        return index >= 0 && index < Citations.Count ? Citations[index] : null;
    }
}
