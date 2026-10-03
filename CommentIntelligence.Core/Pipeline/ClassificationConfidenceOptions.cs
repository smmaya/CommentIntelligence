namespace CommentIntelligence.Core.Pipeline;

public sealed class ClassificationConfidenceOptions
{
    public double MinimumConfidence { get; set; } = 0.25;
    public double MinimumWinnerMargin { get; set; } = 0;

    internal void Validate()
    {
        if (MinimumConfidence is < 0 or > 1 || double.IsNaN(MinimumConfidence))
            throw new ArgumentOutOfRangeException(nameof(MinimumConfidence));
        if (MinimumWinnerMargin is < 0 or > 1 || double.IsNaN(MinimumWinnerMargin))
            throw new ArgumentOutOfRangeException(nameof(MinimumWinnerMargin));
    }
}
