using System.Numerics;

namespace EmbeddingShift.Abstractions;

/// <summary>Small utility methods for 1536 D-Embeddings.</summary>
public static class Vec
{
    public static float[] Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        int n = a.Length;
        var r = new float[n];
        if (Vector.IsHardwareAccelerated && n >= Vector<float>.Count)
        {
            int i = 0;
            int step = Vector<float>.Count;
            for (; i <= n - step; i += step)
            {
                var va = new Vector<float>(a[i..(i + step)]);
                var vb = new Vector<float>(b[i..(i + step)]);
                (va + vb).CopyTo(r, i);
            }
            for (; i < n; i++) r[i] = a[i] + b[i];
        }
        else
        {
            for (int i = 0; i < n; i++) r[i] = a[i] + b[i];
        }
        return r;
    }

    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float sum = 0f;
        if (Vector.IsHardwareAccelerated && a.Length >= Vector<float>.Count)
        {
            int i = 0, step = Vector<float>.Count;
            var acc = Vector<float>.Zero;
            for (; i <= a.Length - step; i += step)
            {
                acc += new Vector<float>(a[i..(i + step)]) * new Vector<float>(b[i..(i + step)]);
            }
            for (int k = 0; k < step; k++) sum += acc[k];
            for (; i < a.Length; i++) sum += a[i] * b[i];
        }
        else
        {
            for (int i = 0; i < a.Length; i++) sum += a[i] * b[i];
        }
        return sum;
    }
}

