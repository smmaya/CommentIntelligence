using System.Globalization;

namespace CommentIntelligence.Core.Text;

public sealed class PolishStemmerProvider : IStemmerProvider
{
    private readonly Lazy<IStemmer> _polish = new(() => new StempelPolishStemmer());

    public IStemmer? GetStemmer(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.Equals("pl", StringComparison.OrdinalIgnoreCase)
            ? _polish.Value
            : null;
}