using System.IO;

namespace EmbeddingShift.Preprocessing.Loading;

public sealed class TxtLoader : IDocumentLoader
{
    public string[] SupportedExtensions => new[] { ".txt", ".log", ".md" };
    public (string Text, IReadOnlyDictionary<string,string>? Meta) Load(string path)
        => (File.ReadAllText(path), new Dictionary<string,string>{{"source", Path.GetFileName(path)}});
}
