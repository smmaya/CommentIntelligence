using System.Globalization;
using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Pipeline;

/// <summary>
/// The single entry point a host app calls to classify a comment end-to-end:
/// sentiment -> stars, content -> label, both -> visibility score.
/// </summary>
public interface ICommentClassificationPipeline
{
    CommentClassification Classify(string text, CultureInfo? culture = null, DateTimeOffset? createdAtUtc = null);
}
