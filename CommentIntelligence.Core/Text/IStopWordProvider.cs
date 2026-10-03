using System.Globalization;

namespace CommentIntelligence.Core.Text;

/// <summary>
/// Supplies the stop-word list for a given culture. K. Tomanek's work (cited in the
/// thesis) highlights stop-list quality as a key driver of Bayes classifier accuracy,
/// so this is pulled out as its own seam rather than hardcoded into the tokenizer.
/// </summary>
public interface IStopWordProvider
{
    IReadOnlySet<string> GetStopWords(CultureInfo culture);
}
