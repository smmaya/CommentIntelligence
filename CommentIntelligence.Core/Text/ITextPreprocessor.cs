using System.Globalization;

namespace CommentIntelligence.Core.Text;

/// <summary>
/// Turns raw comment text into the tokens the Naive Bayes models train/predict on.
/// Implementations are expected to be culture-aware so the package isn't tied to
/// a single language (the thesis model was Polish-only via a fine-tuned RoBERTa;
/// this keeps the algorithm language-agnostic and only the preprocessing step
/// language-specific).
/// </summary>
public interface ITextPreprocessor
{
    IReadOnlyList<string> Tokenize(string text, CultureInfo? culture = null);
}
