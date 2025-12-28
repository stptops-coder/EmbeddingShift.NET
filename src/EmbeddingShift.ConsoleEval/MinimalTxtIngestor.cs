using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Very simple ingestor for demo purposes.
    /// Reads either a single TXT file or all *.txt files in a folder (recursively).
    /// Returns each line together with an increasing order index.
    /// </summary>
    public sealed class MinimalTxtIngestor : IIngestor
    {
        public IEnumerable<(string Text, int Order)> Parse(string path)
        {
            int order = 0;

            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.txt", SearchOption.AllDirectories)
                                             .OrderBy(f => f, System.StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var line in File.ReadLines(file))
                        yield return (line, order++);
                }
            }
            else
            {
                foreach (var line in File.ReadLines(path))
                    yield return (line, order++);
            }
        }
    }
}
