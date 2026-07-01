using System.Globalization;
using System.Text.RegularExpressions;

namespace CommentIntelligence.Core.Text;

/// <summary>
/// Default tokenizer: lowercases, splits on word boundaries (Unicode-letter aware, so it
/// isn't English-only), strips stop words and very short tokens. No stemming in v1 —
/// add a stemmer behind <see cref="ITextPreprocessor"/> per-language later if needed.
/// </summary>
public sealed partial class DefaultTextPreprocessor : ITextPreprocessor
{
    private readonly IStopWordProvider _stopWordProvider;
    private const int MinimumTokenLength = 2;

    public DefaultTextPreprocessor(IStopWordProvider? stopWordProvider = null)
    {
        _stopWordProvider = stopWordProvider ?? new EmbeddedStopWordProvider();
    }

    public IReadOnlyList<string> Tokenize(string text, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        culture ??= CultureInfo.InvariantCulture;
        var stopWords = _stopWordProvider.GetStopWords(culture);

        var lowered = text.ToLower(culture);
        var matches = WordPattern().Matches(lowered);

        var tokens = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            var token = match.Value;
            if (token.Length < MinimumTokenLength)
            {
                continue;
            }

            if (stopWords.Contains(token))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    // \p{L} = any Unicode letter, \p{N} = any Unicode digit. Keeps internal apostrophes
    // (don't, c'est) attached to the word instead of splitting them off.
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:'[\p{L}\p{N}]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}
