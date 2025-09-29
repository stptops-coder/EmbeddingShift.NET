using System;
using System.Runtime.CompilerServices;

namespace EmbeddingShift.Abstractions;
[Obsolete("Prefer EmbeddingShift.Core.Utils.VectorOps; this helper is slated for removal.")]
public static class EmbeddingHelper{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Vector length mismatch.");
        double sum = 0;
        for (int i = 0; i < a.Length; i++) sum += (double)a[i] * b[i];
        return (float)sum;
    }



    /// <summary>Cosine Similarity in [-1,1]. Gibt 0 zurück, wenn ein Vektor Nullnorm hat.</summary>
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Vector length mismatch.");
        var denom = (double)VectorOps.L2Norm(a) * VectorOps.L2Norm(b);
        if (denom == 0) return 0f;
        return Dot(a, b) / (float)denom;
    }

    /// <summary>Normalisiert auf L2-Norm 1 (in-place). Tut nichts bei Nullvektor.</summary>
    public static void NormalizeInPlace(Span<float> v)
    {
        var norm = VectorOps.L2Norm(v);
        if (norm == 0f) return;
        var inv = 1f / norm;
        for (int i = 0; i < v.Length; i++) v[i] *= inv;
    }

    /// <summary>Kopie + Normalisierung.</summary>
    public static float[] NormalizeCopy(ReadOnlySpan<float> v)
    {
        var res = v.ToArray();
        NormalizeInPlace(res);
        return res;
    }

    public static float[] Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Vector length mismatch.");
        var res = new float[a.Length];
        for (int i = 0; i < res.Length; i++) res[i] = a[i] + b[i];
        return res;
    }

    /// <summary>Elementweise Multiplikation (Hadamard-Produkt).</summary>
    public static float[] Multiply(ReadOnlySpan<float> a, ReadOnlySpan<float> factors)
    {
        if (a.Length != factors.Length) throw new ArgumentException("Vector length mismatch.");
        var res = new float[a.Length];
        for (int i = 0; i < res.Length; i++) res[i] = a[i] * factors[i];
        return res;
    }

    public static float[] Scale(ReadOnlySpan<float> v, float s)
    {
        var res = new float[v.Length];
        for (int i = 0; i < res.Length; i++) res[i] = v[i] * s;
        return res;
    }

    /// <summary>Clipped Kopie (optional nützlich für robuste Shifts).</summary>
    public static float[] Clip(ReadOnlySpan<float> v, float min, float max)
    {
        var res = new float[v.Length];
        for (int i = 0; i < res.Length; i++)
        {
            var x = v[i];
            if (x < min) x = min;
            else if (x > max) x = max;
            res[i] = x;
        }
        return res;
    }
}

