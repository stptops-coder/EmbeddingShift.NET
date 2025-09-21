using System;

namespace EmbeddingShift.Abstractions
{
    /// <summary>
    /// Centralized vector operations for embeddings.
    /// Ensures consistent normalization across all components.
    /// </summary>
    public static class VectorOps
    {
        /// <summary>
        /// Computes the L2 norm (Euclidean length) of vector v.
        /// </summary>
        public static float L2Norm(ReadOnlySpan<float> v)
        {
            double sum = 0;
            for (int i = 0; i < v.Length; i++)
                sum += v[i] * v[i];
            return (float)Math.Sqrt(sum);
        }

        /// <summary>
        /// Returns a normalized copy of vector v (unit length).
        /// If v is zero vector, returns a copy unchanged.
        /// </summary>
        public static float[] Normalize(ReadOnlySpan<float> v)
        {
            var len = L2Norm(v);
            if (len == 0) return v.ToArray();

            var result = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
                result[i] = v[i] / len;
            return result;
        }

        /// <summary>
        /// Normalizes vector in-place (unit length).
        /// If zero vector, does nothing.
        /// </summary>
        public static void NormalizeInPlace(Span<float> v)
        {
            var len = L2Norm(v);
            if (len == 0) return;

            for (int i = 0; i < v.Length; i++)
                v[i] /= len;
        }

        /// <summary>
        /// Computes dot product of two vectors.
        /// </summary>
        public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vector dimensions must match.");

            double sum = 0;
            for (int i = 0; i < a.Length; i++)
                sum += a[i] * b[i];
            return (float)sum;
        }

        /// <summary>
        /// Computes cosine similarity between two vectors.
        /// Value ∈ [-1,1], returns 0 if any vector is zero.
        /// </summary>
        public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            var normA = L2Norm(a);
            var normB = L2Norm(b);
            if (normA == 0 || normB == 0) return 0f;

            return Dot(a, b) / (normA * normB);
        }
    }
}
