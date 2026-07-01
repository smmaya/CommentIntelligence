using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Training;

/// <summary>
/// Supplies labeled training examples to a <see cref="Classification.NaiveBayesTrainer"/>.
/// Implementations decide where the data comes from (file path, stream/blob, embedded
/// resource, database, ...) — the trainer doesn't care.
/// </summary>
public interface ITrainingDataProvider
{
    Task<IReadOnlyList<TrainingExample>> LoadAsync(CancellationToken cancellationToken = default);
}
