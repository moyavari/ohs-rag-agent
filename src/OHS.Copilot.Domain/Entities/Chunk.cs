namespace OHS.Copilot.Domain.Entities;

public class Chunk
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = [];

    public static Chunk Create(string text, string title, string section, string sourcePath)
    {
        var chunk = new Chunk
        {
            Id = Guid.NewGuid().ToString(),
            Text = text,
            Title = title,
            Section = section,
            SourcePath = sourcePath,
            Hash = ComputeHash(text)
        };
        return chunk;
    }

    private static string ComputeHash(string text)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
