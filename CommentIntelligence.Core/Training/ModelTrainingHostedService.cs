using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommentIntelligence.Core.Training;

/// <summary>
/// Warms the model registry on app startup by calling <see cref="IModelTrainingService.TrainAllAsync"/>.
/// Registered automatically by <c>AddCommentIntelligence</c> — host apps don't need to touch this.
/// Uses the JSON cache: unchanged languages reload from disk in milliseconds; only changed
/// training data triggers a real retrain pass.
/// </summary>
internal sealed class ModelTrainingHostedService : IHostedService
{
    private readonly IModelTrainingService _trainingService;
    private readonly ILogger<ModelTrainingHostedService> _logger;

    public ModelTrainingHostedService(IModelTrainingService trainingService, ILogger<ModelTrainingHostedService> logger)
    {
        _trainingService = trainingService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CommentIntelligence: starting model training / cache load...");

        try
        {
            await _trainingService.TrainAllAsync(cancellationToken);
            _logger.LogInformation("CommentIntelligence: all models ready.");
        }
        catch (Exception ex)
        {
            // Log but don't crash the host — a partially-trained registry still serves
            // the languages that succeeded. A full failure (no languages) will surface
            // as a NullReferenceException on the first classification call, which is
            // clear enough for a developer to diagnose.
            _logger.LogError(ex, "CommentIntelligence: one or more models failed to train.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}