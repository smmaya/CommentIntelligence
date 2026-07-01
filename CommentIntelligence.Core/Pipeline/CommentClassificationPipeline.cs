using System.Globalization;
using CommentIntelligence.Core.Classification;
using CommentIntelligence.Core.Models;
using CommentIntelligence.Core.Scoring;
using LanguageDetection;

namespace CommentIntelligence.Core.Pipeline;

public sealed class CommentClassificationPipeline : ICommentClassificationPipeline
{
    private readonly ISentimentClassifier _sentimentClassifier;
    private readonly IContentLabelClassifier _contentLabelClassifier;
    private readonly IVisibilityScorer _visibilityScorer;
    private readonly LanguageDetector _langDetector;

    public CommentClassificationPipeline(
        ISentimentClassifier sentimentClassifier,
        IContentLabelClassifier contentLabelClassifier,
        IVisibilityScorer visibilityScorer)
    {
        _sentimentClassifier = sentimentClassifier;
        _contentLabelClassifier = contentLabelClassifier;
        _visibilityScorer = visibilityScorer;
        
        // Detector initialization limiting to the supported languages only
        _langDetector = new LanguageDetector();
        _langDetector.AddLanguages("eng", "pol");
    }

    public CommentClassification Classify(string text, CultureInfo? culture = null, DateTimeOffset? createdAtUtc = null)
    {
        if (culture == null && !string.IsNullOrWhiteSpace(text))
        {
            var detectedIso3 = _langDetector.Detect(text);
            
            culture = detectedIso3 == "pol" 
                ? CultureInfo.GetCultureInfo("pl") 
                : CultureInfo.GetCultureInfo("en");
        }
        
        var sentimentResult = _sentimentClassifier.ClassifyStars(text, culture);
        var contentResult = _contentLabelClassifier.ClassifyContent(text, culture);

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
            PredictedStars = stars,
            SentimentConfidence = sentimentResult.Confidence,
            ContentLabel = contentLabel,
            ContentLabelConfidence = contentResult.Confidence,
            VisibilityScore = visibilityScore,
            DetectedCulture = culture ?? CultureInfo.InvariantCulture
        };
    }
}