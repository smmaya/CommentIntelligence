using System.Globalization;
using System.Text.RegularExpressions;

namespace CommentIntelligence.Core.Text;

/// <summary>
/// Default tokenizer: lowercases, splits on Unicode word boundaries, strips
/// stop words and very short tokens, then optionally stems each token via
/// <see cref="IStemmerProvider"/>. Stemming is skipped when no stemmer is
/// registered for the culture (the default) — behaviour is identical to v1
/// until a stemmer is wired in.
/// </summary>
public sealed partial class DefaultTextPreprocessor : ITextPreprocessor
{
    private readonly IStopWordProvider _stopWordProvider;
    private readonly IStemmerProvider _stemmerProvider;
    private const int MinimumTokenLength = 2;

    public DefaultTextPreprocessor(
        IStopWordProvider? stopWordProvider = null,
        IStemmerProvider? stemmerProvider = null)
    {
        _stopWordProvider = stopWordProvider ?? new EmbeddedStopWordProvider();
        _stemmerProvider = stemmerProvider ?? new NullStemmerProvider();
    }

    public IReadOnlyList<string> Tokenize(string text, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        culture ??= CultureInfo.InvariantCulture;
        var stopWords = _stopWordProvider.GetStopWords(culture);
        var stemmer = _stemmerProvider.GetStemmer(culture);

        var lowered = text.ToLower(culture);
        var matches = WordPattern().Matches(lowered);

        var tokens = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            var token = match.Value;
            if (token.Length < MinimumTokenLength)
                continue;

            if (stopWords.Contains(token))
                continue;

            // Stem after stop-word removal — no point stemming words we discard,
            // and stemmed forms should not be compared against the raw stop-word list.
            var finalToken = stemmer is not null ? stemmer.Stem(token, culture) : token;

            if (finalToken.Length < MinimumTokenLength)
                continue;

            tokens.Add(finalToken);
        }

        foreach (var t in tokens)
        {
            Console.WriteLine(t);
        }

        return tokens;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:'[\p{L}\p{N}]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}