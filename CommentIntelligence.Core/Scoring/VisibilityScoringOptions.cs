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
    
    public double StarModifierWeight { get; set; } = 0.15;

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
    
    /// <summary>Stars at or below this count are eligible for the modifier; 3+ is always neutral.</summary>
    public int LowStarThreshold { get; set; } = 2;

    /// <summary>Low-star + one of these labels = boosted (a useful, substantive complaint).</summary>
    public HashSet<ContentLabel> LowStarBoostLabels { get; set; } = new()
    {
        ContentLabel.Informative,
        ContentLabel.Helpful
    };

    /// <summary>Low-star + one of these labels = dampened (a contentless rant, not a real complaint).</summary>
    public HashSet<ContentLabel> LowStarPenaltyLabels { get; set; } = new()
    {
        ContentLabel.LowQuality,
        ContentLabel.Emotional
    };

    internal void Validate()
    {
        var weights = new[]
        {
            ContentLabelWeight, SentimentConfidenceWeight, ContentLabelConfidenceWeight,
            RecencyWeight, StarModifierWeight
        };

        if (weights.Any(w => double.IsNaN(w) || double.IsInfinity(w) || w < 0))
            throw new ArgumentOutOfRangeException(nameof(weights), "Scoring weights must be finite and non-negative.");

        if (weights.Sum() <= 0)
            throw new ArgumentException("At least one visibility scoring weight must be greater than zero.");

        if (double.IsNaN(RecencyHalfLifeDays) || double.IsInfinity(RecencyHalfLifeDays) || RecencyHalfLifeDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(RecencyHalfLifeDays), "Recency half-life must be positive.");

        if (ContentLabelScores.Any(pair => double.IsNaN(pair.Value) || double.IsInfinity(pair.Value) || pair.Value is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(ContentLabelScores), "Content label scores must be in the range 0..1.");

        if (LowStarThreshold is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(LowStarThreshold), "Low-star threshold must be between 0 and 5.");
    }
}
