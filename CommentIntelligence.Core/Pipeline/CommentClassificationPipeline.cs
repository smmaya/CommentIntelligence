using System.Globalization;
using CommentIntelligence.Core.Classification;
using CommentIntelligence.Core.Models;
using CommentIntelligence.Core.Scoring;
using CommentIntelligence.Core.Text;

namespace CommentIntelligence.Core.Pipeline;

public sealed class CommentClassificationPipeline : ICommentClassificationPipeline
{
    private readonly ISentimentClassifier _sentimentClassifier;
    private readonly IContentLabelClassifier _contentLabelClassifier;
    private readonly IVisibilityScorer _visibilityScorer;
    private readonly ILanguageDetector _languageDetector;
    private readonly IModelRegistry _registry;
    private readonly CultureInfo _defaultCulture;
    private readonly UnsupportedLanguageBehaviour _unsupportedBehaviour;
    
    public IReadOnlyCollection<CultureInfo> SupportedCultures => _registry.SupportedCultures;

    public CommentClassificationPipeline(
        ISentimentClassifier sentimentClassifier,
        IContentLabelClassifier contentLabelClassifier,
        IVisibilityScorer visibilityScorer,
        ILanguageDetector languageDetector,
        IModelRegistry registry,
        CultureInfo defaultCulture,
        UnsupportedLanguageBehaviour unsupportedBehaviour)
    {
        _sentimentClassifier = sentimentClassifier;
        _contentLabelClassifier = contentLabelClassifier;
        _visibilityScorer = visibilityScorer;
        _languageDetector = languageDetector;
        _registry = registry;
        _defaultCulture = defaultCulture;
        _unsupportedBehaviour = unsupportedBehaviour;
    }

    public CommentClassification Classify(string text, CultureInfo? culture = null, DateTimeOffset? createdAtUtc = null)
    {
        var detectedCulture = culture ?? _languageDetector.Detect(text);

        if (detectedCulture is not null && !IsSupported(detectedCulture))
        {
            return _unsupportedBehaviour switch
            {
                UnsupportedLanguageBehaviour.Reject =>
                    CommentClassification.Unsupported(detectedCulture.TwoLetterISOLanguageName),

                UnsupportedLanguageBehaviour.Translate =>
                    throw new NotImplementedException(
                        "UnsupportedLanguageBehaviour.Translate is not yet implemented."),

                _ => CommentClassification.Unsupported(detectedCulture.TwoLetterISOLanguageName)
            };
        }

        var classificationCulture = detectedCulture ?? _defaultCulture;

        var sentimentResult = _sentimentClassifier.ClassifyStars(text, classificationCulture);
        var contentResult = _contentLabelClassifier.ClassifyContent(text, classificationCulture);

        var stars = int.TryParse(sentimentResult.PredictedLabel, out var parsedStars)
            ? Math.Clamp(parsedStars, 1, 5)
            : 3;

        var contentLabel = Enum.TryParse<ContentLabel>(contentResult.PredictedLabel, ignoreCase: true, out var parsedLabel)
            ? parsedLabel
            : ContentLabel.Unknown;

        var timestamp = createdAtUtc ?? DateTimeOffset.UtcNow;
        var visibilityScore = _visibilityScorer.Score(contentLabel, contentResult.Confidence, sentimentResult.Confidence, timestamp);

        return new CommentClassification
        {
            IsSupported = true,
            PredictedStars = stars,
            SentimentConfidence = sentimentResult.Confidence,
            ContentLabel = contentLabel,
            ContentLabelConfidence = contentResult.Confidence,
            VisibilityScore = visibilityScore,
            DetectedCulture = classificationCulture
        };
    }

    private bool IsSupported(CultureInfo culture) =>
        _registry.SupportedCultures.Any(c =>
            c.TwoLetterISOLanguageName.Equals(
                culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));
}