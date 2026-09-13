using System;

namespace Cisharpai.Rag;

/// <summary>
/// Brute-force vector operations for in-memory retrieval scoring.
/// </summary>
public static class VectorMath
{
    /// <summary>
    /// Computes the dot product of two vectors. Throws if dimensions differ.
    /// </summary>
    public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        ThrowIfDimensionMismatch(a.Length, b.Length);

        var sum = 0f;
        for (var i = 0; i < a.Length; i++)
            sum += a[i] * b[i];
        return sum;
    }

    /// <inheritdoc cref="DotProduct(ReadOnlySpan{float}, ReadOnlySpan{float})"/>
    public static float DotProduct(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return DotProduct((ReadOnlySpan<float>)a, b);
    }

    /// <inheritdoc cref="DotProduct(ReadOnlySpan{float}, ReadOnlySpan{float})"/>
    public static float DotProduct(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return DotProduct(ToArray(a), ToArray(b));
    }

    /// <summary>
    /// Computes the cosine similarity of two vectors (range −1 to 1).
    /// Returns 0 when either vector has zero magnitude. Throws if dimensions differ.
    /// </summary>
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        ThrowIfDimensionMismatch(a.Length, b.Length);

        var dot = 0.0;
        var normA = 0.0;
        var normB = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0.0 ? 0f : (float)(dot / denominator);
    }

    /// <inheritdoc cref="CosineSimilarity(ReadOnlySpan{float}, ReadOnlySpan{float})"/>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return CosineSimilarity((ReadOnlySpan<float>)a, b);
    }

    /// <inheritdoc cref="CosineSimilarity(ReadOnlySpan{float}, ReadOnlySpan{float})"/>
    public static float CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return CosineSimilarity(ToArray(a), ToArray(b));
    }

    /// <summary>
    /// Returns a new unit-length vector. The input is not modified.
    /// A zero-magnitude vector is returned unchanged (all zeros) to prevent NaN propagation.
    /// </summary>
    public static float[] Normalize(ReadOnlySpan<float> vector)
    {
        var result = new float[vector.Length];
        vector.CopyTo(result);
        NormalizeInPlace(result);
        return result;
    }

    /// <inheritdoc cref="Normalize(ReadOnlySpan{float})"/>
    public static float[] Normalize(IReadOnlyList<float> vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Normalize((ReadOnlySpan<float>)ToArray(vector));
    }

    /// <summary>
    /// Normalizes the vector to unit length in place, mutating the input.
    /// A zero-magnitude vector is left unchanged (all zeros) to prevent NaN propagation.
    /// </summary>
    public static void NormalizeInPlace(Span<float> vector)
    {
        var sumSq = 0.0;
        for (var i = 0; i < vector.Length; i++)
            sumSq += (double)vector[i] * vector[i];

        if (sumSq == 0.0)
            return;

        var invNorm = 1.0 / Math.Sqrt(sumSq);
        for (var i = 0; i < vector.Length; i++)
            vector[i] = (float)(vector[i] * invNorm);
    }

    /// <summary>
    /// Returns the top <paramref name="k"/> candidates by cosine similarity to
    /// <paramref name="query"/>, in descending score order.
    /// Each result contains the candidate index and its similarity score.
    /// </summary>
    public static (int Index, float Score)[] TopK(
        ReadOnlySpan<float> query,
        ReadOnlySpan<float[]> candidates,
        int k)
    {
        if (k <= 0)
            throw new ArgumentOutOfRangeException(nameof(k), k, "k must be positive.");

        var scored = new (int Index, float Score)[candidates.Length];
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is null)
                throw new ArgumentNullException(nameof(candidates), $"Candidate at index {i} is null.");
            scored[i] = (i, CosineSimilarity(query, candidates[i]));
        }

        Array.Sort(scored, (x, y) => y.Score.CompareTo(x.Score));

        var resultLength = Math.Min(k, scored.Length);
        var result = new (int Index, float Score)[resultLength];
        Array.Copy(scored, result, resultLength);
        return result;
    }

    /// <inheritdoc cref="TopK(ReadOnlySpan{float}, ReadOnlySpan{float[]}, int)"/>
    public static (int Index, float Score)[] TopK(
        IReadOnlyList<float> query,
        IReadOnlyList<float[]> candidates,
        int k)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        var queryArr = ToArray(query);
        if (candidates is float[][] jagged)
            return TopK((ReadOnlySpan<float>)queryArr, (ReadOnlySpan<float[]>)jagged, k);
        var candidateArr = new float[candidates.Count][];
        for (var i = 0; i < candidates.Count; i++)
            candidateArr[i] = candidates[i]
                ?? throw new ArgumentNullException(nameof(candidates), $"Candidate at index {i} is null.");
        return TopK((ReadOnlySpan<float>)queryArr, (ReadOnlySpan<float[]>)candidateArr, k);
    }

    private static void ThrowIfDimensionMismatch(int lengthA, int lengthB)
    {
        if (lengthA != lengthB)
            throw new ArgumentException(
                $"Vector dimensions must match: {lengthA} vs {lengthB}.");
    }

    private static float[] ToArray(IReadOnlyList<float> list)
    {
        if (list is float[] arr)
            return arr;

        var result = new float[list.Count];
        for (var i = 0; i < list.Count; i++)
            result[i] = list[i];
        return result;
    }
}
