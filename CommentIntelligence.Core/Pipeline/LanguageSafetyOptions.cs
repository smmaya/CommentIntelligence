using System.Globalization;

namespace CommentIntelligence.Core.Pipeline;

public sealed class LanguageSafetyOptions
{
    private readonly Dictionary<string, LanguageSafetyTerms> _terms = new(StringComparer.OrdinalIgnoreCase);

    public void AddLanguage(
        string languageCode,
        IEnumerable<string> abusiveTerms,
        IEnumerable<string> strongNegativePhrases)
    {
        var key = CultureInfo.GetCultureInfo(languageCode).TwoLetterISOLanguageName;
        _terms[key] = new LanguageSafetyTerms(
            abusiveTerms.ToHashSet(StringComparer.OrdinalIgnoreCase),
            strongNegativePhrases.Where(phrase => !string.IsNullOrWhiteSpace(phrase))
                .ToArray());
    }

    internal bool ContainsExplicitAbuse(string text, CultureInfo culture) =>
        GetTerms(culture).AbusiveTerms.Any(term =>
            text.Contains(term, StringComparison.OrdinalIgnoreCase));

    internal bool ContainsStrongNegativeSignal(string text, CultureInfo culture) =>
        GetTerms(culture).StrongNegativePhrases.Any(phrase =>
            text.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    private LanguageSafetyTerms GetTerms(CultureInfo culture) =>
        _terms.TryGetValue(culture.TwoLetterISOLanguageName, out var terms)
            ? terms
            : LanguageSafetyTerms.Empty;

    private sealed record LanguageSafetyTerms(
        IReadOnlySet<string> AbusiveTerms,
        IReadOnlyList<string> StrongNegativePhrases)
    {
        public static LanguageSafetyTerms Empty { get; } = new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());
    }
}
