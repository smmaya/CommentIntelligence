namespace CommentIntelligence.Core.Scoring;

public sealed class VisibilityScorer : IVisibilityScorer
{
    private readonly VisibilityScoringOptions _options;

    public VisibilityScorer(VisibilityScoringOptions? options = null)
    {
        _options = options ?? new VisibilityScoringOptions();
        _options.Validate();
    }

    public double Score(
        ContentLabel contentLabel,
        double contentLabelConfidence,
        double sentimentConfidence,
        int stars,
        DateTimeOffset createdAtUtc)
    {
        var labelScore = _options.ContentLabelScores.GetValueOrDefault(contentLabel, 0.3);

        var ageInDays = Math.Max((DateTimeOffset.UtcNow - createdAtUtc).TotalDays, 0);
        var halfLife = Math.Max(_options.RecencyHalfLifeDays, 1);
        var recencyScore = Math.Pow(0.5, ageInDays / halfLife);

        var starModifierScore = StarModifier(stars, contentLabel);

        var rawScore =
            _options.ContentLabelWeight * labelScore +
            _options.ContentLabelConfidenceWeight * contentLabelConfidence +
            _options.SentimentConfidenceWeight * sentimentConfidence +
            _options.RecencyWeight * recencyScore +
            _options.StarModifierWeight * starModifierScore;

        var totalWeight = _options.ContentLabelWeight
                          + _options.ContentLabelConfidenceWeight
                          + _options.SentimentConfidenceWeight
                          + _options.RecencyWeight
                          + _options.StarModifierWeight;

        var score = totalWeight > 0 ? rawScore / totalWeight : 0;
        var labelCap = contentLabel switch
        {
            ContentLabel.Hateful => 0.1,
            ContentLabel.Tendentious => 0.25,
            ContentLabel.LowQuality => 0.35,
            _ => 1.0
        };

        return Math.Clamp(Math.Min(score, labelCap), 0, 1);
    }
    
    /// <summary>
    /// 1.0 = boosted, 0.0 = dampened, 0.5 = neutral (no effect on the weighted average).
    /// Only applies at or below <see cref="VisibilityScoringOptions.LowStarThreshold"/> —
    /// a 3-5 star review is never adjusted by this term.
    /// </summary>
    private double StarModifier(int stars, ContentLabel contentLabel)
    {
        if (stars > _options.LowStarThreshold)
        {
            return 0.5;
        }

        if (_options.LowStarBoostLabels.Contains(contentLabel))
        {
            return 1.0;
        }

        if (_options.LowStarPenaltyLabels.Contains(contentLabel))
        {
            return 0.0;
        }

        return 0.5; // Tendentious/Hateful/Unknown: already penalized via ContentLabelScores
    }
}
