using System.Globalization;
using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Classification;

/// <summary>
/// Resolves the correct per-culture model from <see cref="IModelRegistry"/> on every
/// call, so a hot-swapped (retrained) model takes effect immediately for the next
/// classification — no restart, no caching of the model reference here.
/// </summary>
public sealed class NaiveBayesSentimentClassifier : ISentimentClassifier
{
    private readonly IModelRegistry _registry;
    private readonly NaiveBayesPredictor _predictor;

    public NaiveBayesSentimentClassifier(IModelRegistry registry, NaiveBayesPredictor predictor)
    {
        _registry = registry;
        _predictor = predictor;
    }

    public ClassificationResult ClassifyStars(string text, CultureInfo? culture = null)
    {
        var models = _registry.Resolve(culture);
        return _predictor.Predict(text, models.SentimentModel, culture);
    }
}