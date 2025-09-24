namespace EmbeddingShift.Preprocessing.Loading;

public interface IDocumentLoader
{
    string[] SupportedExtensions { get; }
    /// Loads raw text + optional metadata. Return null/empty if not supported.
    (string Text, IReadOnlyDictionary<string,string>? Meta) Load(string path);
}
