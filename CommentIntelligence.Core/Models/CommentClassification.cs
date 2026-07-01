namespace CommentIntelligence.Core.Models;

/// <summary>
/// The full, system-computed classification of a comment: sentiment (-> stars)
/// and content quality (-> label) are independent axes, combined into a single
/// visibility score used for sorting/ranking.
/// </summary>
public sealed class CommentClassification
{
    /// <summary>System-derived star rating (1-5), based purely on the text.</summary>
    public required int PredictedStars { get; init; }

    public required double SentimentConfidence { get; init; }

    public required ContentLabel ContentLabel { get; init; }

    public required double ContentLabelConfidence { get; init; }
    
    public System.Globalization.CultureInfo DetectedCulture { get; set; } = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>
    /// Normalized 0..1 score used to rank/sort comments by usefulness to a buyer.
    /// High for informative/helpful comments regardless of star rating; low for
    /// hateful, tendentious, or content-free comments regardless of star rating.
    /// </summary>
    public required double VisibilityScore { get; init; }
}
