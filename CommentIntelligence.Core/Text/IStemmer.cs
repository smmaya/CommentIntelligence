using System.Globalization;

namespace CommentIntelligence.Core.Text;

/// <summary>
/// Reduces an already-tokenized word to its stem so inflected forms
/// ("running", "ran", "runs") map to the same token during classification.
/// Implementations are culture-specific — register one per language that
/// benefits from stemming (heavily inflected languages like PL, CS, HU gain
/// the most; EN gains less but it still helps with sparse training data).
/// </summary>
public interface IStemmer
{
    string Stem(string token, CultureInfo culture);
}