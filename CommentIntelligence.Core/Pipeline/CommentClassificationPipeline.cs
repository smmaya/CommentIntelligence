using System.Globalization;
using System.Text.RegularExpressions;
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
    private readonly ClassificationConfidenceOptions _confidenceOptions;
    private readonly LanguageSafetyOptions _safetyOptions;
    private readonly DomainRelevanceOptions _domainRelevanceOptions;
    // Short text can overlap with generic training words without describing
    // the reviewed product. Abstain before producing misleading stars/labels.
    private const int MinimumInputTokenCount = 5;
    
    public IReadOnlyCollection<CultureInfo> SupportedCultures => _registry.SupportedCultures;

    public CommentClassificationPipeline(
        ISentimentClassifier sentimentClassifier,
        IContentLabelClassifier contentLabelClassifier,
        IVisibilityScorer visibilityScorer,
        ILanguageDetector languageDetector,
        IModelRegistry registry,
        CultureInfo defaultCulture,
        UnsupportedLanguageBehaviour unsupportedBehaviour,
        ClassificationConfidenceOptions? confidenceOptions = null,
        LanguageSafetyOptions? safetyOptions = null,
        DomainRelevanceOptions? domainRelevanceOptions = null)
    {
        _sentimentClassifier = sentimentClassifier;
        _contentLabelClassifier = contentLabelClassifier;
        _visibilityScorer = visibilityScorer;
        _languageDetector = languageDetector;
        _registry = registry;
        _defaultCulture = defaultCulture;
        _unsupportedBehaviour = unsupportedBehaviour;
        _confidenceOptions = confidenceOptions ?? new ClassificationConfidenceOptions();
        _confidenceOptions.Validate();
        _safetyOptions = safetyOptions ?? new LanguageSafetyOptions();
        _domainRelevanceOptions = domainRelevanceOptions ?? new DomainRelevanceOptions();
    }

    public CommentClassification Classify(string text, CultureInfo? culture = null, DateTimeOffset? createdAtUtc = null)
    {
        if (!_registry.IsReady)
        {
            throw new InvalidOperationException("Comment classification models are not ready.");
        }

        var detectedCulture = culture ?? _languageDetector.Detect(text);

        if (detectedCulture is not null && !IsSupported(detectedCulture))
        {
            return _unsupportedBehaviour switch
            {
                UnsupportedLanguageBehaviour.Reject =>
                    CommentClassification.Unsupported(detectedCulture.TwoLetterISOLanguageName),

                UnsupportedLanguageBehaviour.Translate =>
                    throw new NotSupportedException(
                        "Translation is not configured for unsupported languages."),

                _ => CommentClassification.Unsupported(detectedCulture.TwoLetterISOLanguageName)
            };
        }

        var classificationCulture = detectedCulture ?? _defaultCulture;
        var explicitAbuse = _safetyOptions.ContainsExplicitAbuse(text, classificationCulture);

        var inputTokenCount = CountInputTokens(text);
        if ((inputTokenCount < MinimumInputTokenCount && !explicitAbuse) ||
            (inputTokenCount < 2) ||
            !_domainRelevanceOptions.IsRelevant(text, classificationCulture))
        {
            return new CommentClassification
            {
                IsSupported = true,
                IsLowInformation = true,
                IsUsefulForBuyer = false,
                PredictedStars = 0,
                SentimentConfidence = 0,
                ContentLabel = ContentLabel.Unknown,
                ContentLabelConfidence = 0,
                InformationConfidence = 0,
                VisibilityScore = 0,
                DetectedCulture = classificationCulture
            };
        }

        var sentimentResult = _sentimentClassifier.ClassifyStars(text, classificationCulture);
        var contentResult = _contentLabelClassifier.ClassifyContent(text, classificationCulture);
        var strongNegative = _safetyOptions.ContainsStrongNegativeSignal(text, classificationCulture);

        if (!sentimentResult.HasUsableTokens || !contentResult.HasUsableTokens)
        {
            return new CommentClassification
            {
                IsSupported = true,
                IsLowInformation = true,
                IsUsefulForBuyer = false,
                PredictedStars = 0,
                SentimentConfidence = 0,
                ContentLabel = ContentLabel.Unknown,
                ContentLabelConfidence = 0,
                InformationConfidence = 0,
                VisibilityScore = 0,
                DetectedCulture = classificationCulture
            };
        }

        if (!explicitAbuse && (IsAmbiguous(sentimentResult) || IsAmbiguous(contentResult)))
        {
            return new CommentClassification
            {
                IsSupported = true,
                IsLowInformation = true,
                IsUsefulForBuyer = false,
                PredictedStars = 0,
                SentimentConfidence = sentimentResult.Confidence,
                ContentLabel = ContentLabel.Unknown,
                ContentLabelConfidence = contentResult.Confidence,
                InformationConfidence = 0,
                VisibilityScore = 0,
                DetectedCulture = classificationCulture
            };
        }

        var stars = explicitAbuse || strongNegative
            ? 1
            : int.TryParse(sentimentResult.PredictedLabel, out var parsedStars)
            ? Math.Clamp(parsedStars, 1, 5)
            : 3;

        var predictedContentLabel = explicitAbuse
            ? ContentLabel.Hateful
            : Enum.TryParse<ContentLabel>(contentResult.PredictedLabel, ignoreCase: true, out var parsedLabel)
            ? parsedLabel
            : ContentLabel.Unknown;
        var hasProductDetail = CountInputTokens(text) >= 5;
        var hasPromotionalSignal = ContainsPromotionalSignal(text);
        var contentLabel = predictedContentLabel switch
        {
            ContentLabel.Hateful when !explicitAbuse =>
                hasProductDetail ? ContentLabel.Informative : ContentLabel.LowQuality,
            ContentLabel.Tendentious when !hasPromotionalSignal =>
                hasProductDetail ? ContentLabel.Informative : ContentLabel.Emotional,
            _ => predictedContentLabel
        };
        var contentLabelProbabilities = contentResult.ClassProbabilities
            .Select(pair => (Success: Enum.TryParse<ContentLabel>(pair.Key, true, out var label), Label: label, Probability: pair.Value))
            .Where(item => item.Success && item.Label != ContentLabel.Unknown)
            .ToDictionary(item => item.Label, item => item.Probability);
        var contentLabels = contentLabelProbabilities
            // Positive/descriptive labels can be useful secondary signals with
            // modest evidence. Safety and quality labels require stronger
            // evidence to avoid turning figurative language into moderation hits.
            .Where(pair => pair.Key != ContentLabel.Hateful &&
                           (pair.Key != ContentLabel.Tendentious || hasPromotionalSignal) &&
                           pair.Value >= SecondaryLabelThreshold(pair.Key))
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .Take(3)
            .ToList();
        if (contentLabel != ContentLabel.Unknown && !contentLabels.Contains(contentLabel))
            contentLabels.Insert(0, contentLabel);

        var timestamp = createdAtUtc ?? DateTimeOffset.UtcNow;
        var visibilityScore = _visibilityScorer.Score(contentLabel, contentResult.Confidence, sentimentResult.Confidence, stars, timestamp);
        var isPromotional = contentLabel == ContentLabel.Tendentious &&
                            hasPromotionalSignal;
        var isUsefulForBuyer = !explicitAbuse &&
                               hasProductDetail &&
                               (contentLabel is ContentLabel.Informative or ContentLabel.Helpful ||
                                stars <= 3);

        return new CommentClassification
        {
            IsSupported = true,
            IsAbusive = explicitAbuse,
            IsPromotional = isPromotional,
            IsUsefulForBuyer = isUsefulForBuyer,
            InformationConfidence = hasProductDetail ? Math.Min(1, CountInputTokens(text) / 12d) : 0,
            PredictedStars = stars,
            SentimentConfidence = strongNegative ? 1 : sentimentResult.Confidence,
            ContentLabel = contentLabel,
            ContentLabelConfidence = explicitAbuse ? 1 : contentResult.Confidence,
            ContentLabels = explicitAbuse ? [ContentLabel.Hateful] : contentLabels,
            ContentLabelProbabilities = contentLabelProbabilities,
            VisibilityScore = visibilityScore,
            DetectedCulture = classificationCulture
        };
    }

    private bool IsSupported(CultureInfo culture) =>
        _registry.SupportedCultures.Any(c =>
            c.TwoLetterISOLanguageName.Equals(
                culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));

    private static int CountInputTokens(string text) =>
        Regex.Matches(text, @"[\p{L}\p{N}]+").Count;

    private static bool ContainsPromotionalSignal(string text)
    {
        var promotionalTerms = new[]
        {
            "best", "perfect", "unbeatable", "only", "everyone should",
            "must buy", "najleps", "ideal", "bezkonkur", "każdy powinien",
            "każdy musi", "jedyny"
        };

        return promotionalTerms.Any(term =>
            text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static double SecondaryLabelThreshold(ContentLabel label) =>
        label is ContentLabel.Hateful or ContentLabel.Tendentious or ContentLabel.LowQuality
            ? 0.50
            : 0.05;

    private bool IsAmbiguous(ClassificationResult result)
    {
        var runnerUp = result.ClassProbabilities.Values
            .OrderByDescending(value => value)
            .Skip(1)
            .FirstOrDefault();
        return result.Confidence < _confidenceOptions.MinimumConfidence ||
               result.Confidence - runnerUp < _confidenceOptions.MinimumWinnerMargin;
    }
}