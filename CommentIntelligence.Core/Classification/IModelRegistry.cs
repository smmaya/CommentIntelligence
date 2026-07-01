using System.Globalization;

namespace CommentIntelligence.Core.Classification;

/// <summary>
/// Holds the currently active trained models, keyed by language. Reads never block on
/// writes: <see cref="Set"/> atomically swaps the reference for a culture so in-flight
/// classification calls keep using the old model until the swap completes, and callers
/// immediately after see the new one — no restart required.
/// </summary>
public interface IModelRegistry
{
    /// <summary>
    /// Resolves the model set for <paramref name="culture"/>. Falls back to the registry's
    /// configured default culture if no model was trained for the requested one (e.g. a
    /// comment in a language with no training data yet) rather than throwing.
    /// </summary>
    ClassifierModelSet Resolve(CultureInfo? culture);

    void Set(CultureInfo culture, ClassifierModelSet models);

    IReadOnlyCollection<CultureInfo> SupportedCultures { get; }
}