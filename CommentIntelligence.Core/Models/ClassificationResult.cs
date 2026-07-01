namespace CommentIntelligence.Core.Models;

/// <summary>
/// Raw output of a single Naive Bayes prediction: the winning label,
/// the model's confidence in it, and the full probability distribution
/// over all known labels (useful for debugging / the test harness).
/// </summary>
public sealed class ClassificationResult
{
    public required string PredictedLabel { get; init; }

    /// <summary>Probability assigned to the predicted label, in the range 0..1.</summary>
    public required double Confidence { get; init; }

    public IReadOnlyDictionary<string, double> ClassProbabilities { get; init; } =
        new Dictionary<string, double>();
}
