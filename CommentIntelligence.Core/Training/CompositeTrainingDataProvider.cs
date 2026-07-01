using CommentIntelligence.Core.Models;

namespace CommentIntelligence.Core.Training;

/// <summary>
/// Merges training examples from several providers — e.g. a shipped base dataset
/// plus a site-specific CSV of corrections/additions, without retraining logic
/// needing to know about either source individually.
/// </summary>
public sealed class CompositeTrainingDataProvider : ITrainingDataProvider
{
    private readonly IReadOnlyList<ITrainingDataProvider> _providers;

    public CompositeTrainingDataProvider(params ITrainingDataProvider[] providers)
    {
        _providers = providers;
    }

    public async Task<IReadOnlyList<TrainingExample>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<TrainingExample>();
        foreach (var provider in _providers)
        {
            all.AddRange(await provider.LoadAsync(cancellationToken));
        }

        return all;
    }
}
