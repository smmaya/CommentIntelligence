using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Scoring;

/// <summary>
/// Tunable weights for <see cref="VisibilityScorer"/>. Defaults favor content quality
/// heavily over recency, and treat Hateful/Tendentious/LowQuality as low-visibility
/// regardless of star rating — the goal is "useful for a buying decision", not "flattering
/// to the seller" and not "matches what the buyer felt".
/// </summary>
public sealed class VisibilityScoringOptions
{
    public double ContentLabelWeight { get; set; } = 0.5;
    public double SentimentConfidenceWeight { get; set; } = 0.2;
    public double ContentLabelConfidenceWeight { get; set; } = 0.2;
    public double RecencyWeight { get; set; } = 0.1;

    /// <summary>Half-life, in days, used for the recency decay component.</summary>
    public double RecencyHalfLifeDays { get; set; } = 30;

    /// <summary>Base desirability score (0..1) per content label, before confidence weighting.</summary>
    public Dictionary<ContentLabel, double> ContentLabelScores { get; set; } = new()
    {
        [ContentLabel.Informative] = 1.0,
        [ContentLabel.Helpful] = 0.9,
        [ContentLabel.Emotional] = 0.4,
        [ContentLabel.Tendentious] = 0.2,
        [ContentLabel.Hateful] = 0.0,
        [ContentLabel.LowQuality] = 0.1,
        [ContentLabel.Unknown] = 0.3
    };
}
