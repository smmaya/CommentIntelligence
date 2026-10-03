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

        await _trainingService.TrainAllAsync(cancellationToken);
        _logger.LogInformation("CommentIntelligence: all models ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}