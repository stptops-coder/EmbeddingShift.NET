// ReSharper disable once CheckNamespace
namespace EmbeddingShift.Preprocessing.Chunking;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents a single text chunk with metadata.
/// </summary>
public sealed record SimpleTextChunk(
    int Index,
    int Start,
    int Length,
    string Content);

/// <summary>
/// Simple whitespace-aware chunker with configurable chunk size and overlap.
/// This class does not depend on any project-specific interfaces and can be
/// wrapped or adapted to IChunker later.
/// </summary>
public sealed class SimpleTextChunker
{
    /// <summary>
    /// Splits the given text into overlapping chunks.
    /// </summary>
    /// <param name="text">Full input text.</param>
    /// <param name="maxChunkLength">
    /// Maximum length of a single chunk in characters (after normalization).
    /// </param>
    /// <param name="overlap">
    /// Number of characters to overlap between two consecutive chunks.
    /// </param>
    public IReadOnlyList<SimpleTextChunk> Chunk(
        string text,
        int maxChunkLength = 1000,
        int overlap = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<SimpleTextChunk>();
        }

        if (maxChunkLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxChunkLength),
                "maxChunkLength must be > 0.");
        }

        if (overlap < 0 || overlap >= maxChunkLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlap),
                "overlap must be >= 0 and < maxChunkLength.");
        }

        var normalized = NormalizeLineEndings(text);
        var chunks = new List<SimpleTextChunk>();

        int currentStart = 0;
        int index = 0;
        int totalLength = normalized.Length;

        while (currentStart < totalLength)
        {
            int remaining   = totalLength - currentStart;
            int currentSize = Math.Min(maxChunkLength, remaining);
            int end         = currentStart + currentSize;

            // Try to avoid cutting words in half by moving the end to the last whitespace
            if (end < totalLength)
            {
                int lastWhitespace = LastWhitespaceBefore(normalized, end);
                // Only move back if this still gives us a reasonably sized chunk
                if (lastWhitespace > currentStart + maxChunkLength / 2)
                {
                    end         = lastWhitespace + 1; // include the whitespace character
                    currentSize = end - currentStart;
                }
            }

            string content = normalized.Substring(currentStart, currentSize);

            chunks.Add(new SimpleTextChunk(
                Index: index,
                Start: currentStart,
                Length: currentSize,
                Content: content));

            if (end >= totalLength)
            {
                break;
            }

            // Compute next start with overlap
            int nextStart = end - overlap;
            if (nextStart <= currentStart)
            {
                // Safety guard: ensure progress
                nextStart = end;
            }

            currentStart = nextStart;
            index++;
        }

        return chunks;
    }

    private static string NormalizeLineEndings(string text)
    {
        // Normalize to '\n' only to make character offsets stable.
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static int LastWhitespaceBefore(string text, int position)
    {
        for (int i = position - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }
}