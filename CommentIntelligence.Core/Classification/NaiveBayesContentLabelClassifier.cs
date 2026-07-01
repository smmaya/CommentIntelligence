using System.Globalization;
using CommentIntelligence.Core.Models;

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
        return _predictor.Predict(text, models.ContentLabelModel, culture);
    }
}