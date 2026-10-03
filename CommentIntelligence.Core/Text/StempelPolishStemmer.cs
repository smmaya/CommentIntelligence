using System.Globalization;
using Lucene.Analysis.Pl;
using Lucene.Analysis.Stempel;

namespace CommentIntelligence.Core.Text;

public sealed class StempelPolishStemmer : IStemmer
{
    private readonly StempelStemmer _stemmer;

    public StempelPolishStemmer()
    {
        // Same embedded resource PolishAnalyzer itself loads internally —
        // ships inside LuceneSharp.Analysis.Stempel.dll, no file path needed.
        using var stream = typeof(PolishAnalyzer).Assembly
                               .GetManifestResourceStream("Lucene.Pl.stemmer_20000.tbl")
                           ?? throw new InvalidOperationException(
                               "Embedded resource 'Lucene.Pl.stemmer_20000.tbl' not found in LuceneSharp.Analysis.Stempel.");

        _stemmer = new StempelStemmer(stream);
    }

    public string Stem(string token, CultureInfo culture)
    {
        // Mirrors StempelFilter's own DefaultMinLength=3 gate — the real
        // filter leaves short words unchanged rather than stemming them.
        if (token.Length < StempelFilter.DefaultMinLength)
            return token;

        var stemmed = _stemmer.Stem(token.AsSpan());
        return stemmed?.ToString() ?? token; // null = "no stem found" → keep original
    }
}