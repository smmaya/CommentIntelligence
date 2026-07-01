using System.Globalization;

namespace CommentIntelligence.Core.Models;

/// <summary>
/// A single comment/review submitted by a user, plus its system-computed classification.
/// </summary>
public sealed class Comment
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Text { get; init; }

    public string? AuthorId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>Populated after the comment has been run through the classification pipeline.</summary>
    public CommentClassification? Classification { get; set; }
}
