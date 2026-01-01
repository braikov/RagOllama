namespace Rag.Core.Math;

public static class VectorMath
{
    /// <summary>
    /// Computes cosine similarity between two vectors. Returns 0 when magnitudes are zero.
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same dimensions.");
        }

        if (a.Length == 0)
        {
            return 0d;
        }

        double dot = 0;
        double magA = 0;
        double magB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denom = System.Math.Sqrt(magA) * System.Math.Sqrt(magB);
        if (denom == 0)
        {
            return 0d;
        }

        return dot / denom;
    }
}
