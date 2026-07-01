using System.Globalization;
using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Classification;

/// <summary>
/// Classifies what kind of comment this is (informative, helpful, emotional,
/// tendentious, hateful, low-quality) — independent of star rating. This is the
/// axis that drives visibility/ranking, not sentiment.
/// </summary>
public interface IContentLabelClassifier
{
    ClassificationResult ClassifyContent(string text, CultureInfo? culture = null);
}
