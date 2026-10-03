using System.Globalization;

namespace CommentIntelligence.Core.Text;

/// <summary>
/// Resolves the correct <see cref="IStemmer"/> for a given culture.
/// Returns null if no stemmer is registered for that language — the
/// preprocessor skips stemming gracefully rather than throwing.
/// </summary>
public interface IStemmerProvider
{
    IStemmer? GetStemmer(CultureInfo culture);
}