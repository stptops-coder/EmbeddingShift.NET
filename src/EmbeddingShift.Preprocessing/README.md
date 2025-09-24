# EmbeddingShift.Preprocessing

This project provides the **document preprocessing pipeline** used before embeddings are generated.

## Steps

1. **Loading & Transformation**
   - IDocumentLoader (e.g., TxtLoader)
   - ITransformer (e.g., Normalizer)
   - Converts raw files into normalized plain text

2. **Chunking**
   - IChunker (e.g., FixedChunker)
   - Splits normalized text into chunks suitable for embeddings

## Structure
- Loading/
- Transform/
- Chunking/
- PreprocessPipeline.cs

## Example
`csharp
var pipeline = new PreprocessPipeline(
    new IDocumentLoader[] { new TxtLoader() },
    new Normalizer(),
    new FixedChunker(1000, 100));

foreach (var (chunk, meta) in pipeline.Run("sample.txt"))
{
    Console.WriteLine(chunk);
}
