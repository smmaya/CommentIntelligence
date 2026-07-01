using System.Globalization;
using CommentIntelligence.Core.Training;

namespace CommentIntelligence.Core.Classification;

/// <summary>
/// One language's worth of training configuration: a culture plus the two CSV
/// (or stream/composite) providers that train its sentiment and content-label models.
/// Register one of these per supported language in <c>AddCommentIntelligence</c>.
/// </summary>
public sealed class LanguageTrainingSet
{
    public required CultureInfo Culture { get; init; }

    public required ITrainingDataProvider SentimentTrainingDataProvider { get; init; }

    public required ITrainingDataProvider ContentLabelTrainingDataProvider { get; init; }
}