using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Scoring;

public interface IVisibilityScorer
{
    /// <summary>Returns a normalized 0..1 usefulness score for ranking/sorting.</summary>
    double Score(ContentLabel contentLabel, double contentLabelConfidence, double sentimentConfidence, DateTimeOffset createdAtUtc);
}
