using System.Globalization;

namespace CommentIntelligence.Core.Text;

/// <summary>
/// Default stemmer provider — returns null for every culture, meaning
/// no stemming is applied. Swap in a real implementation via DI when
/// you're ready to add Snowball or another stemmer.
/// </summary>
public sealed class NullStemmerProvider : IStemmerProvider
{
    public IStemmer? GetStemmer(CultureInfo culture) => null;
}