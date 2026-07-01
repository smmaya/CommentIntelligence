using System.Globalization;
using CommentIntelligence.Core.Classification;
using LanguageDetection;

namespace CommentIntelligence.Core.Text;

public sealed class LanguageDetectionAiDetector : ILanguageDetector
{
    private static readonly Dictionary<string, string> Iso639_1ToIso639_3 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "eng", ["pl"] = "pol", ["de"] = "deu", ["fr"] = "fra",
        ["es"] = "spa", ["it"] = "ita", ["nl"] = "nld", ["pt"] = "por",
        ["ru"] = "rus", ["zh"] = "cmn", ["ja"] = "jpn", ["ko"] = "kor",
        ["ar"] = "ara", ["sv"] = "swe", ["da"] = "dan", ["fi"] = "fin",
        ["nb"] = "nor", ["cs"] = "ces", ["sk"] = "slk", ["hu"] = "hun",
        ["ro"] = "ron", ["tr"] = "tur", ["uk"] = "ukr",
    };

    private static readonly Dictionary<string, string> Iso639_3ToIso639_1 =
        Iso639_1ToIso639_3.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    private readonly IModelRegistry _registry;
    private readonly CultureInfo _defaultCulture;
    private LanguageDetector _detector;

    public LanguageDetectionAiDetector(IModelRegistry registry, CultureInfo defaultCulture)
    {
        _registry = registry;
        _defaultCulture = defaultCulture;
        _detector = new LanguageDetector();
        _detector.AddAllLanguages();
    }

    public CultureInfo? Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var iso3 = _detector.Detect(text);
            if (iso3 is null) return null;
            return Iso639_3ToIso639_1.TryGetValue(iso3, out var iso1)
                ? CultureInfo.GetCultureInfo(iso1)
                : null;
        }
        catch { return null; }
    }
}