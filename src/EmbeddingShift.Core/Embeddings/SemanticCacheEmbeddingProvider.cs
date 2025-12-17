using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Embeddings;

/// <summary>
/// Decorator for <see cref="IEmbeddingProvider"/> that adds a lightweight semantic cache.
///
/// - Exact cache: SHA-256 over normalized text (whitespace/punctuation-insensitive).
/// - Optional approximate cache: SimHash over normalized tokens, with band buckets (4x16 bits).
///
/// Important: Cached vectors are treated as immutable. This provider returns clones for cache hits
/// to avoid accidental in-place mutations leaking back into the cache.
/// </summary>
public sealed class SemanticCacheEmbeddingProvider : IEmbeddingProvider
{
    private readonly IEmbeddingProvider _inner;
    private readonly SemanticCacheOptions _options;

    private readonly ConcurrentDictionary<string, CacheEntry> _byKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<uint, ConcurrentBag<string>> _bands = new();
    private readonly ConcurrentQueue<string> _fifo = new();

    private long _hitExact;
    private long _hitApprox;
    private long _miss;
    private long _evicted;

    public string Name => $"{_inner.Name}+semantic-cache";

    public SemanticCacheEmbeddingProvider(IEmbeddingProvider inner, SemanticCacheOptions? options = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options ?? new SemanticCacheOptions();
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        text ??= string.Empty;

        var normalized = TextNorm.Normalize(text);
        var key = Hashing.Sha256Hex(normalized);

        if (_byKey.TryGetValue(key, out var exact))
        {
            Interlocked.Increment(ref _hitExact);
            return exact.CloneVector();
        }

        ulong simHash = 0;

        if (_options.EnableApproximate)
        {
            simHash = SimHash.Compute64(normalized);

            var approxKey = TryFindApproxKey(simHash);
            if (approxKey is not null && _byKey.TryGetValue(approxKey, out var approx))
            {
                Interlocked.Increment(ref _hitApprox);

                // Alias exact key -> reuse vector (immutable) for fast next time.
                TryInsert(new CacheEntry(key, simHash, approx.Vector));

                return approx.CloneVector();
            }
        }

        Interlocked.Increment(ref _miss);

        var vec = await _inner.GetEmbeddingAsync(text).ConfigureAwait(false);
        vec ??= Array.Empty<float>();

        // Cache a clone; return original (avoid extra copy).
        var stored = (float[])vec.Clone();
        TryInsert(new CacheEntry(key, simHash, stored));

        return vec;
    }

    public SemanticCacheStats GetStats()
        => new(
            Entries: _byKey.Count,
            HitExact: Interlocked.Read(ref _hitExact),
            HitApprox: Interlocked.Read(ref _hitApprox),
            Miss: Interlocked.Read(ref _miss),
            Evicted: Interlocked.Read(ref _evicted));

    private void TryInsert(CacheEntry entry)
    {
        if (!_byKey.TryAdd(entry.Key, entry))
            return;

        _fifo.Enqueue(entry.Key);

        if (_options.EnableApproximate && entry.SimHash != 0)
        {
            foreach (var bandKey in Banding.GetBandKeys(entry.SimHash))
                _bands.GetOrAdd(bandKey, _ => new ConcurrentBag<string>()).Add(entry.Key);
        }

        EnforceMax();
    }

    private void EnforceMax()
    {
        if (_options.MaxEntries <= 0) return;

        while (_byKey.Count > _options.MaxEntries && _fifo.TryDequeue(out var oldest))
        {
            if (_byKey.TryRemove(oldest, out _))
                Interlocked.Increment(ref _evicted);
        }
    }

    private string? TryFindApproxKey(ulong simHash)
    {
        string? bestKey = null;
        var best = _options.HammingThreshold + 1;

        foreach (var bandKey in Banding.GetBandKeys(simHash))
        {
            if (!_bands.TryGetValue(bandKey, out var bag))
                continue;

            foreach (var candidateKey in bag)
            {
                if (!_byKey.TryGetValue(candidateKey, out var entry))
                    continue; // stale

                if (entry.SimHash == 0)
                    continue;

                var d = SimHash.HammingDistance(simHash, entry.SimHash);
                if (d < best)
                {
                    best = d;
                    bestKey = candidateKey;
                    if (best == 0) return bestKey;
                }
            }
        }

        return best <= _options.HammingThreshold ? bestKey : null;
    }
}

public sealed record SemanticCacheStats(int Entries, long HitExact, long HitApprox, long Miss, long Evicted);

public sealed class SemanticCacheOptions
{
    /// <summary>0 = unlimited</summary>
    public int MaxEntries { get; init; } = 20_000;

    public bool EnableApproximate { get; init; } = true;

    /// <summary>0..64</summary>
    public int HammingThreshold { get; init; } = 3;

    public static SemanticCacheOptions FromEnvironment()
        => new()
        {
            MaxEntries = ReadInt("EMBEDDING_SEMANTIC_CACHE_MAX", 20_000),
            EnableApproximate = IsTruthy(Environment.GetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_APPROX"), true),
            HammingThreshold = ReadInt("EMBEDDING_SEMANTIC_CACHE_HAMMING", 3),
        };

    public static bool IsTruthy(string? v, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(v)) return defaultValue;
        var s = v.Trim();
        return s.Equals("1", StringComparison.OrdinalIgnoreCase)
               || s.Equals("true", StringComparison.OrdinalIgnoreCase)
               || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || s.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadInt(string name, int def)
    {
        var raw = (Environment.GetEnvironmentVariable(name) ?? string.Empty).Trim();
        return int.TryParse(raw, out var v) ? v : def;
    }
}

internal sealed class CacheEntry
{
    public string Key { get; }
    public ulong SimHash { get; }
    public float[] Vector { get; }

    public CacheEntry(string key, ulong simHash, float[] vector)
    {
        Key = key;
        SimHash = simHash;
        Vector = vector;
    }

    public float[] CloneVector() => (float[])Vector.Clone();
}

internal static class TextNorm
{
    // Conservative stopword list (keep domain terms).
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "from", "has", "have",
        "in", "into", "is", "it", "its", "of", "on", "or", "that", "the", "their", "then",
        "this", "to", "was", "were", "with", "what", "when", "where", "which", "who", "why",
        "section", "policy", "coverage", "claim", "claims", "cover", "covered", "provide", "provides",
        "include", "includes", "including", "exclusion", "exclusions", "limit", "limits"
    };

    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool lastSpace = true;

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastSpace = false;
            }
            else
            {
                if (!lastSpace)
                {
                    sb.Append(' ');
                    lastSpace = true;
                }
            }
        }

        return sb.ToString().Trim();
    }

    public static IEnumerable<string> Tokens(string normalized)
    {
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in parts)
        {
            if (raw.Length < 3) continue;
            if (IsAllDigits(raw)) continue;

            var t = Stem(raw);
            if (t.Length < 3) continue;
            if (Stopwords.Contains(t)) continue;

            yield return t;
        }
    }

    private static bool IsAllDigits(string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (!char.IsDigit(s[i])) return false;
        return true;
    }

    private static string Stem(string token)
    {
        if (token.Length <= 4) return token;

        if (token.EndsWith("ing", StringComparison.Ordinal) && token.Length > 5) return token[..^3];
        if (token.EndsWith("ed", StringComparison.Ordinal) && token.Length > 4) return token[..^2];
        if (token.EndsWith("es", StringComparison.Ordinal) && token.Length > 4) return token[..^2];
        if (token.EndsWith("s", StringComparison.Ordinal) && token.Length > 3) return token[..^1];

        return token;
    }
}

internal static class Hashing
{
    public static string Sha256Hex(string s)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));

        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

internal static class SimHash
{
    public static ulong Compute64(string normalizedText)
    {
        Span<int> acc = stackalloc int[64];
        acc.Clear();

        foreach (var token in TextNorm.Tokens(normalizedText))
        {
            var h = Fnv1a64(token);

            for (int bit = 0; bit < 64; bit++)
            {
                if (((h >> bit) & 1UL) != 0) acc[bit] += 1;
                else acc[bit] -= 1;
            }
        }

        ulong sig = 0;
        for (int bit = 0; bit < 64; bit++)
            if (acc[bit] > 0) sig |= 1UL << bit;

        return sig;
    }

    public static int HammingDistance(ulong a, ulong b)
        => BitOperations.PopCount(a ^ b);

    private static ulong Fnv1a64(string s)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offset;
        for (int i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            hash ^= (byte)(ch & 0xFF);
            hash *= prime;
            hash ^= (byte)(ch >> 8);
            hash *= prime;
        }

        return hash;
    }
}

internal static class Banding
{
    // 4 bands * 16 bits = 64 bits
    public static IEnumerable<uint> GetBandKeys(ulong simHash)
    {
        for (int band = 0; band < 4; band++)
        {
            var segment = (uint)((simHash >> (band * 16)) & 0xFFFFUL);
            yield return ((uint)band << 16) | segment;
        }
    }
}
