namespace CommentIntelligence.Core.Pipeline;

public enum UnsupportedLanguageBehaviour
{
    /// <summary>
    /// Return a classification with IsSupported=false and zero VisibilityScore.
    /// The host app decides what to show the user — block submission, show a warning, etc.
    /// </summary>
    Reject,

    /// <summary>
    /// Translate the text to DefaultCulture before classifying (v2 — not yet implemented).
    /// </summary>
    Translate
}