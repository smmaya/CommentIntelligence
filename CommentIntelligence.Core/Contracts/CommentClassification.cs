using System.Globalization;

namespace CommentIntelligence.Core.Contracts;

public sealed class CommentClassification
{
    /// <summary>False when the comment's language has no trained model in the registry.</summary>
    public bool IsSupported { get; init; } = true;

    /// <summary>True when the text had no usable tokens or confidence was insufficient.</summary>
    public bool IsLowInformation { get; init; }

    /// <summary>True when the text contains a configured abusive term.</summary>
    public bool IsAbusive { get; init; }

    /// <summary>True when the text is likely promotional or exaggerated.</summary>
    public bool IsPromotional { get; init; }

    /// <summary>True when the review contains enough product-specific detail to help a buyer.</summary>
    public bool IsUsefulForBuyer { get; init; }

    /// <summary>Confidence in the amount of useful product information present in the review.</summary>
    public required double InformationConfidence { get; init; }

    /// <summary>
    /// Two-letter ISO language code of the unsupported language, when IsSupported is false.
    /// Null otherwise.
    /// </summary>
    public string? UnsupportedLanguageCode { get; init; }

    /// <summary>System-derived star rating (1-5), based purely on the text.</summary>
    public required int PredictedStars { get; init; }

    public required double SentimentConfidence { get; init; }

    public required ContentLabel ContentLabel { get; init; }

    public required double ContentLabelConfidence { get; init; }

    public IReadOnlyList<ContentLabel> ContentLabels { get; init; } = Array.Empty<ContentLabel>();

    public IReadOnlyDictionary<ContentLabel, double> ContentLabelProbabilities { get; init; } =
        new Dictionary<ContentLabel, double>();

    public CultureInfo DetectedCulture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Normalized 0..1 score used to rank/sort comments by usefulness to a buyer.
    /// Zero when IsSupported is false.
    /// </summary>
    public required double VisibilityScore { get; init; }

    /// <summary>Convenience factory for an unsupported-language result.</summary>
    public static CommentClassification Unsupported(string? languageCode) => new()
    {
        IsSupported = false,
        UnsupportedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "unknown" : languageCode,
        PredictedStars = 0,
        SentimentConfidence = 0,
        ContentLabel = ContentLabel.Unknown,
        ContentLabelConfidence = 0,
        InformationConfidence = 0,
        VisibilityScore = 0
    };
}