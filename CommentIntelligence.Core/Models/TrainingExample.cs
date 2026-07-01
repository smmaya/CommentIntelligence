namespace CommentIntelligence.Core.Models;

/// <summary>
/// A single labeled example used to train a Naive Bayes model.
/// The same shape is reused for both the sentiment classifier (Label = "1".."5")
/// and the content-label classifier (Label = a <see cref="ContentLabel"/> name).
/// </summary>
public sealed class TrainingExample
{
    public required string Text { get; init; }

    public required string Label { get; init; }
}
