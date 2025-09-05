namespace OHS.Copilot.Domain.ValueObjects;

public record Citation
{
    public string Id { get; init; } = string.Empty;
    public double Score { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Url { get; init; }
    public string Text { get; init; } = string.Empty;

    public static Citation Create(string id, double score, string title, string text, string? url = null)
    {
        return new Citation
        {
            Id = id,
            Score = score,
            Title = title,
            Text = text,
            Url = url
        };
    }
}
