using System.Globalization;

namespace CommentIntelligence.Core.Text;

/// <summary>
/// Detects the language of a piece of text and returns the matching <see cref="CultureInfo"/>.
/// The default implementation uses the registered languages from the model registry so it
/// only ever returns cultures that actually have a trained model behind them.
/// Host apps can swap this out (e.g. to always pass the storefront's active language)
/// by registering a custom implementation before calling AddCommentIntelligence.
/// </summary>
public interface ILanguageDetector
{
    /// <summary>
    /// Returns the best-match culture for <paramref name="text"/>, or null if detection
    /// is inconclusive (caller falls back to DefaultCulture in that case).
    /// </summary>
    CultureInfo? Detect(string text);
}