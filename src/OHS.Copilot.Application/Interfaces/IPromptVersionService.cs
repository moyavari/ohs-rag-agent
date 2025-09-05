namespace OHS.Copilot.Application.Interfaces;

public interface IPromptVersionService
{
    Task<string> StorePromptAsync(string promptContent, string promptName = "default", CancellationToken cancellationToken = default);
    Task<PromptVersion?> GetPromptByHashAsync(string hash, CancellationToken cancellationToken = default);
    Task<List<PromptVersion>> GetPromptHistoryAsync(string promptName, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetAllPromptHashesAsync(CancellationToken cancellationToken = default);
}

public class PromptVersion
{
    public string Hash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
    public string? Description { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];

    public static PromptVersion Create(string hash, string name, string content, int version = 1, string? description = null)
    {
        return new PromptVersion
        {
            Hash = hash,
            Name = name,
            Content = content,
            Version = version,
            Description = description
        };
    }

    public static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
