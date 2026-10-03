using System.Globalization;
using LanguageDetection;

namespace CommentIntelligence.Core.Text;

public sealed class LanguageDetectionAiDetector : ILanguageDetector
{
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

        var polish = _registry.SupportedCultures.FirstOrDefault(culture =>
            culture.TwoLetterISOLanguageName.Equals("pl", StringComparison.OrdinalIgnoreCase));
        if (polish is not null && ContainsPolishMarker(text))
            return polish;

        if (text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length < 3)
        {
            // Language libraries cannot make a reliable decision from one- or
            // two-word text. Let the pipeline use its configured default culture.
            return null;
        }

        try
        {
            var iso3 = _detector.Detect(text);
            if (string.IsNullOrWhiteSpace(iso3)) return null;
            var supportedCulture = _registry.SupportedCultures.FirstOrDefault(culture =>
                culture.ThreeLetterISOLanguageName.Equals(iso3, StringComparison.OrdinalIgnoreCase));
            if (supportedCulture is not null)
                return supportedCulture;

            return CultureInfo.GetCultures(CultureTypes.AllCultures)
                .FirstOrDefault(culture =>
                    culture.ThreeLetterISOLanguageName.Equals(iso3, StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidOperationException) { return null; }
    }

    private static bool ContainsPolishMarker(string text) =>
        text.Contains("polecam", StringComparison.OrdinalIgnoreCase) ||
        text.Any(character => character is 'ą' or 'ć' or 'ę' or 'ł' or 'ń' or 'ó' or 'ś' or 'ź' or 'ż');
}