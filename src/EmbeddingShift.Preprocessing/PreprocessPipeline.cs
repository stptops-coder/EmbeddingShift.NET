using EmbeddingShift.Preprocessing.Loading;
using EmbeddingShift.Preprocessing.Transform;
using EmbeddingShift.Preprocessing.Chunking;

namespace EmbeddingShift.Preprocessing;

/// Simple pipeline: Load  Transform  Chunk
public sealed class PreprocessPipeline
{
    private readonly IReadOnlyList<IDocumentLoader> _loaders;
    private readonly ITransformer _transformer;
    private readonly IChunker _chunker;

    public PreprocessPipeline(
        IEnumerable<IDocumentLoader> loaders,
        ITransformer transformer,
        IChunker chunker)
    {
        _loaders = loaders.ToList();
        _transformer = transformer;
        _chunker = chunker;
    }

    public IEnumerable<(string Chunk, IReadOnlyDictionary<string,string>? Meta)> Run(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var loader = _loaders.FirstOrDefault(l => l.SupportedExtensions.Contains(ext));
        if (loader is null) yield break;

        var (text, meta) = loader.Load(path);
        var clean = _transformer.Transform(text);
        foreach (var c in _chunker.Chunk(clean))
            yield return (c, meta);
    }
}
