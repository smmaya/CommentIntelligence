using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace CommentIntelligence.Core.Text;

/// <summary>
/// Loads stop-word lists embedded as text resources under Text/StopWords/{two-letter-iso-language}.txt.
/// Falls back to English if no list exists for the requested culture, so the preprocessor
/// never breaks for an unsupported language — it just won't filter as aggressively.
/// </summary>
public sealed class EmbeddedStopWordProvider : IStopWordProvider
{
    private const string FallbackLanguage = "en";
    private static readonly ConcurrentDictionary<string, IReadOnlySet<string>> Cache = new();
    private readonly Assembly _assembly = typeof(EmbeddedStopWordProvider).Assembly;

    public IReadOnlySet<string> GetStopWords(CultureInfo culture)
    {
        var language = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        return Cache.GetOrAdd(language, LoadForLanguage);
    }

    private IReadOnlySet<string> LoadForLanguage(string language)
    {
        var words = LoadResource(language) ?? LoadResource(FallbackLanguage);
        return words ?? new HashSet<string>();
    }

    private HashSet<string>? LoadResource(string language)
    {
        var resourceName = $"{_assembly.GetName().Name}.Text.StopWords.{language}.txt";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
            {
                words.Add(trimmed);
            }
        }

        return words;
    }
}
