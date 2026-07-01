using System.Globalization;

namespace CommentIntelligence.Core.Models;

public sealed class CommentClassification
{
    /// <summary>False when the comment's language has no trained model in the registry.</summary>
    public bool IsSupported { get; init; } = true;

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

    public CultureInfo DetectedCulture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Normalized 0..1 score used to rank/sort comments by usefulness to a buyer.
    /// Zero when IsSupported is false.
    /// </summary>
    public required double VisibilityScore { get; init; }

    /// <summary>Convenience factory for an unsupported-language result.</summary>
    public static CommentClassification Unsupported(string languageCode) => new()
    {
        IsSupported = false,
        UnsupportedLanguageCode = languageCode,
        PredictedStars = 0,
        SentimentConfidence = 0,
        ContentLabel = ContentLabel.Unknown,
        ContentLabelConfidence = 0,
        VisibilityScore = 0
    };
}