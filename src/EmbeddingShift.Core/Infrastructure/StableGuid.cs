using System;
using System.Security.Cryptography;
using System.Text;

namespace EmbeddingShift.Core.Infrastructure
{
    /// <summary>
    /// Stable GUID generator for deterministic identifiers derived from textual keys.
    /// This is useful for file-based persistence where ids must be reproducible.
    /// </summary>
    public static class StableGuid
    {
        public static Guid FromString(string value)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

            Span<byte> guidBytes = stackalloc byte[16];
            bytes.AsSpan(0, 16).CopyTo(guidBytes);

            // RFC 4122 variant
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
            // Version 5 (name-based), see RFC 4122
            guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);

            return new Guid(guidBytes);
        }
    }
}
