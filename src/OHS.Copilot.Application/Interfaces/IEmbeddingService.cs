namespace OHS.Copilot.Application.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
    int GetEmbeddingDimensions();
    string GetModelName();
}

public class EmbeddingResult
{
    public float[] Vector { get; set; } = [];
    public string Text { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string Model { get; set; } = string.Empty;

    public static EmbeddingResult Create(float[] vector, string text, int tokenCount, string model)
    {
        return new EmbeddingResult
        {
            Vector = vector,
            Text = text,
            TokenCount = tokenCount,
            Model = model
        };
    }
}
