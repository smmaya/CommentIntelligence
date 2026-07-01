using System.Globalization;
using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Classification;

/// <summary>
/// Derives a star rating (1-5) directly from comment text. There is no separate
/// user-submitted star input in this design — the model's prediction IS the rating.
/// </summary>
public interface ISentimentClassifier
{
    ClassificationResult ClassifyStars(string text, CultureInfo? culture = null);
}
