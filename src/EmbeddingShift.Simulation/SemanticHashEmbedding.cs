using System;
using System.Collections.Generic;
using System.Text;

namespace EmbeddingShift.Simulation;

/// <summary>
/// Deterministic, lightweight text-to-vector simulation that preserves a basic notion
/// of semantic locality: texts sharing many tokens (and optionally character n-grams)
/// will tend to produce more similar vectors.
///
/// This is NOT meant to be an exact replica of any vendor embedding space, but it is
/// stable, reproducible, and "embedding-like" in the sense that it maps text to a dense
/// float vector where cosine similarity correlates with textual overlap.
/// </summary>
public static class SemanticHashEmbedding
{
    // A deliberately small stopword list. Keep it conservative to avoid erasing domain terms.
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "from", "has", "have",
        "in", "into", "is", "it", "its", "of", "on", "or", "that", "the", "their", "then",
        "this", "to", "was", "were", "with", "what", "when", "where", "which", "who", "why",
        // very common “document boilerplate” words
        "section", "policy", "coverage", "claim", "claims", "cover", "covered", "provide", "provides",
        "include", "includes", "including", "exclusion", "exclusions", "limit", "limits"
    };

    /// <summary>
    /// Create a deterministic embedding vector for the given text.
    /// </summary>
    public static float[] Create(
        string text,
        int embeddingSize,
        int tokenFeaturesPerToken = 8,
        bool includeCharNGrams = true,
        int charNGramSize = 3,
        int charFeaturesPerNGram = 2)
    {
        if (embeddingSize <= 0) throw new ArgumentOutOfRangeException(nameof(embeddingSize));
        if (tokenFeaturesPerToken <= 0) throw new ArgumentOutOfRangeException(nameof(tokenFeaturesPerToken));
        if (charNGramSize <= 1) throw new ArgumentOutOfRangeException(nameof(charNGramSize));
        if (charFeaturesPerNGram <= 0) throw new ArgumentOutOfRangeException(nameof(charFeaturesPerNGram));

        var vec = new float[embeddingSize];
        if (string.IsNullOrWhiteSpace(text)) return vec;

        foreach (var token in Tokenize(text))
        {
            var h = Fnv1a32(token);
            AddHashedFeatures(vec, h, tokenFeaturesPerToken);
        }

        if (includeCharNGrams)
        {
            foreach (var ngram in GetCharNGrams(text, charNGramSize))
            {
                var h = Fnv1a32(ngram);
                AddHashedFeatures(vec, h, charFeaturesPerNGram);
            }
        }

        NormalizeInPlace(vec);
        return vec;
    }

    private static void AddHashedFeatures(float[] vec, uint baseHash, int count)
    {
        for (int k = 0; k < count; k++)
        {
            var h = Mix32(baseHash, (uint)k);

            var idx = (int)(h % (uint)vec.Length);
            var sign = ((h & 0x8000_0000u) != 0) ? 1f : -1f;
            var mag = 0.5f + ((h & 0x0000_FFFFu) / 65535f);

            vec[idx] += sign * mag;
        }
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            else sb.Append(' ');
        }

        var parts = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in parts)
        {
            if (raw.Length < 3) continue;
            if (IsAllDigits(raw)) continue;

            var token = SimpleStem(raw);
            if (token.Length < 3) continue;
            if (Stopwords.Contains(token)) continue;

            yield return token;
        }
    }

    private static IEnumerable<string> GetCharNGrams(string text, int n)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }

        var s = sb.ToString();
        if (s.Length < n) yield break;

        for (int i = 0; i <= s.Length - n; i++)
            yield return s.Substring(i, n);
    }

    private static bool IsAllDigits(string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (!char.IsDigit(s[i])) return false;
        return true;
    }

    private static string SimpleStem(string token)
    {
        if (token.Length <= 4) return token;

        if (token.EndsWith("ing", StringComparison.Ordinal) && token.Length > 5) return token[..^3];
        if (token.EndsWith("ed", StringComparison.Ordinal) && token.Length > 4) return token[..^2];
        if (token.EndsWith("es", StringComparison.Ordinal) && token.Length > 4) return token[..^2];
        if (token.EndsWith("s", StringComparison.Ordinal) && token.Length > 3) return token[..^1];

        return token;
    }

    private static uint Fnv1a32(string s)
    {
        const uint offset = 2166136261u;
        const uint prime = 16777619u;

        var hash = offset;
        for (int i = 0; i < s.Length; i++)
        {
            hash ^= s[i];
            hash *= prime;
        }

        return hash;
    }

    private static uint Mix32(uint x, uint salt)
    {
        x ^= salt + 0x9E3779B9u;
        x ^= x >> 16;
        x *= 0x7FEB352Du;
        x ^= x >> 15;
        x *= 0x846CA68Bu;
        x ^= x >> 16;
        return x;
    }

    private static void NormalizeInPlace(float[] v)
    {
        double sumSq = 0;
        for (var i = 0; i < v.Length; i++)
            sumSq += (double)v[i] * v[i];

        if (sumSq <= 0) return;

        var inv = 1.0 / Math.Sqrt(sumSq);
        for (var i = 0; i < v.Length; i++)
            v[i] = (float)(v[i] * inv);
    }
}
