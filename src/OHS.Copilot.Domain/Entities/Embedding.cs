namespace OHS.Copilot.Domain.Entities;

public class Embedding
{
    public string ChunkId { get; set; } = string.Empty;
    public float[] Vector { get; set; } = [];
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static Embedding Create(string chunkId, float[] vector, string model)
    {
        return new Embedding
        {
            ChunkId = chunkId,
            Vector = vector,
            Model = model
        };
    }

    public double CosineSimilarity(Embedding other)
    {
        return ComputeCosineSimilarity(Vector, other.Vector);
    }

    private static double ComputeCosineSimilarity(float[] a, float[] b)
    {
        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
