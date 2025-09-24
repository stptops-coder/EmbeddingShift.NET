using System.Text.RegularExpressions;

namespace EmbeddingShift.Preprocessing.Transform;

/// Basic cleanup: normalize whitespace, trim, collapse multiple blanks.
public sealed class Normalizer : ITransformer
{
    public string Transform(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Replace('\t',' ').Replace('\r',' ');
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }
}
