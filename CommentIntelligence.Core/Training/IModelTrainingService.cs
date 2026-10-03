using System.Globalization;

namespace CommentIntelligence.Core.Training;

/// <summary>
/// Trains (or retrains) the per-culture models and swaps them into the active
/// <see cref="Classification.IModelRegistry"/>. This is the single seam an admin
/// endpoint, a scheduled job, or a future "retrain after N new comments" DB-driven
/// trigger should all call through — none of them need to know about CSV files,
/// caching, or the registry directly.
/// </summary>
public interface IModelTrainingService
{
    /// <summary>
    /// Trains every configured language. Uses the JSON cache when the training data
    /// hasn't changed (fingerprint match); only retrains languages whose data changed.
    /// Called once at startup.
    /// </summary>
    Task TrainAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a retrain for one culture, bypassing the cache fingerprint check, and
    /// swaps the new model into the registry. This is what an admin "retrain now"
    /// action or a future DB-driven trigger should call.
    /// </summary>
    Task RetrainAsync(CultureInfo culture, CancellationToken cancellationToken = default);

    /// <summary>Forces a retrain for every configured language.</summary>
    Task RetrainAllAsync(CancellationToken cancellationToken = default);
}