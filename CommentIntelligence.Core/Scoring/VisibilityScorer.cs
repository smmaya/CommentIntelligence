using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Scoring;

public sealed class VisibilityScorer : IVisibilityScorer
{
    private readonly VisibilityScoringOptions _options;

    public VisibilityScorer(VisibilityScoringOptions? options = null)
    {
        _options = options ?? new VisibilityScoringOptions();
    }

    public double Score(ContentLabel contentLabel, double contentLabelConfidence, double sentimentConfidence, DateTimeOffset createdAtUtc)
    {
        var labelScore = _options.ContentLabelScores.GetValueOrDefault(contentLabel, 0.3);

        var ageInDays = Math.Max((DateTimeOffset.UtcNow - createdAtUtc).TotalDays, 0);
        var halfLife = Math.Max(_options.RecencyHalfLifeDays, 1);
        var recencyScore = Math.Pow(0.5, ageInDays / halfLife);

        var rawScore =
            _options.ContentLabelWeight * labelScore +
            _options.ContentLabelConfidenceWeight * contentLabelConfidence +
            _options.SentimentConfidenceWeight * sentimentConfidence +
            _options.RecencyWeight * recencyScore;

        var totalWeight = _options.ContentLabelWeight
                           + _options.ContentLabelConfidenceWeight
                           + _options.SentimentConfidenceWeight
                           + _options.RecencyWeight;

        return totalWeight > 0 ? rawScore / totalWeight : 0;
    }
}
