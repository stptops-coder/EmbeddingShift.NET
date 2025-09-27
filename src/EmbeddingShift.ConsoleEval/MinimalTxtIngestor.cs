using EmbeddingShift.Abstractions;
using System.Collections.Generic;
using System.IO;

namespace EmbeddingShift.ConsoleEval
{
    public sealed class MinimalTxtIngestor : IIngestor
    {
        // Interface: IEnumerable<(string Text, int Order)> Parse(string path);
        public IEnumerable<(string Text, int Order)> Parse(string path)
        {
            int order = 0;

            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.txt", SearchOption.AllDirectories))
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
