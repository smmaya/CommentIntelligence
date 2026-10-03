using System.Globalization;

namespace CommentIntelligence.Core.Classification;

public sealed class NaiveBayesContentLabelClassifier : IContentLabelClassifier
{
    private readonly IModelRegistry _registry;
    private readonly NaiveBayesPredictor _predictor;

    public NaiveBayesContentLabelClassifier(IModelRegistry registry, NaiveBayesPredictor predictor)
    {
        _registry = registry;
        _predictor = predictor;
    }

    public ClassificationResult ClassifyContent(string text, CultureInfo? culture = null)
    {
        var models = _registry.Resolve(culture);
        if (models.ContentLabelModels.Count == 0)
            return _predictor.Predict(text, models.ContentLabelModel, culture);

        var results = models.ContentLabelModels
            .ToDictionary(pair => pair.Key, pair => _predictor.Predict(text, pair.Value, culture));
        var usable = results.Values.FirstOrDefault()?.HasUsableTokens == true;
        if (!usable)
            return new ClassificationResult
            {
                PredictedLabel = string.Empty,
                Confidence = 0,
                HasUsableTokens = false
            };

        var probabilities = results.ToDictionary(
            pair => pair.Key.ToString(),
            pair => pair.Value.ClassProbabilities.GetValueOrDefault("positive"));
        var best = probabilities.OrderByDescending(pair => pair.Value).First();
        return new ClassificationResult
        {
            PredictedLabel = best.Key,
            Confidence = best.Value,
            HasUsableTokens = true,
            ClassProbabilities = probabilities
        };
    }
}