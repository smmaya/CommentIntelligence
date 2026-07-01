namespace CommentIntelligence.Core.Models;

/// <summary>
/// What kind of comment this is, independent of whether it reads as positive or negative.
/// A 1-star review can still be <see cref="Informative"/> and highly useful; a 5-star
/// review can be <see cref="LowQuality"/> and useless for a buying decision.
/// </summary>
public enum ContentLabel
{
    Unknown = 0,
    Informative,
    Helpful,
    Emotional,
    Tendentious,
    Hateful,
    LowQuality
}
