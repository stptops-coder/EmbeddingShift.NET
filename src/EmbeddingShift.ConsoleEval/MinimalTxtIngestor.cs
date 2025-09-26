using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval;

public sealed class MinimalTxtIngestor : IIngestor
{
    public IEnumerable<(string Text, int Order)> Parse(string filePath)
    {
        int i = 0;
        foreach (var line in File.ReadLines(filePath))
            yield return (line, i++);
    }
}
