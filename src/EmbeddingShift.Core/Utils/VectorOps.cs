using System;

namespace EmbeddingShift.Core.Utils
{
    public static class VectorOps
    {
        public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length) throw new ArgumentException("Vector length mismatch.");
            double acc = 0d;
            for (int i = 0; i < a.Length; i++) acc += a[i] * b[i];
            return (float)acc;
        }
        public static float Norm2(ReadOnlySpan<float> v)
        {
            double acc = 0d;
            for (int i = 0; i < v.Length; i++) acc += (double)v[i] * v[i];
            return (float)Math.Sqrt(acc);
        }
        public static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float eps = 1e-12f)
        {
            var na = Norm2(a); var nb = Norm2(b);
            if (na <= eps || nb <= eps) return 0f;
            return Dot(a, b) / (na * nb);
        }
        public static float[] Normalize(ReadOnlySpan<float> v, float eps = 1e-12f)
        {
            var n = Norm2(v); var res = new float[v.Length];
            if (n <= eps) return res;
            var inv = 1f / n;
            for (int i = 0; i < v.Length; i++) res[i] = v[i] * inv;
            return res;
        }
        public static float[] Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length) throw new ArgumentException("Vector length mismatch.");
            var y = new float[a.Length];
            for (int i = 0; i < a.Length; i++) y[i] = a[i] + b[i];
            return y;
        }
        public static float[] Mul(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length) throw new ArgumentException("Vector length mismatch.");
            var y = new float[a.Length];
            for (int i = 0; i < a.Length; i++) y[i] = a[i] * b[i];
            return y;
        }
    }
}
