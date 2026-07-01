namespace CommentIntelligence.Core.Classification;

/// <summary>Both trained models for a single language/culture, swapped atomically on retrain.</summary>
public sealed class ClassifierModelSet
{
    public required NaiveBayesModel SentimentModel { get; init; }

    public required NaiveBayesModel ContentLabelModel { get; init; }
}