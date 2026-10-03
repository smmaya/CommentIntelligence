using System.Globalization;

namespace CommentIntelligence.Core.Pipeline;

public sealed class DomainRelevanceOptions
{
    private readonly Dictionary<string, HashSet<string>> _termsByLanguage =
        new(StringComparer.OrdinalIgnoreCase);

    public int MinimumMatchingTerms { get; set; } = 1;

    public void AddLanguage(string languageCode, IEnumerable<string> terms)
    {
        var language = CultureInfo.GetCultureInfo(languageCode).TwoLetterISOLanguageName;
        _termsByLanguage[language] = new HashSet<string>(terms, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsRelevant(string text, CultureInfo culture)
    {
        if (!_termsByLanguage.TryGetValue(culture.TwoLetterISOLanguageName, out var terms) ||
            terms.Count == 0)
            return true;

        var matches = terms.Count(term =>
            text.Contains(term, StringComparison.OrdinalIgnoreCase));
        return matches >= MinimumMatchingTerms;
    }
}
