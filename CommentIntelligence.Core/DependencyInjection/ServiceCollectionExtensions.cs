using System.Globalization;
using CommentIntelligence.Core.Classification;
using CommentIntelligence.Core.Classification.Persistence;
using CommentIntelligence.Core.Pipeline;
using CommentIntelligence.Core.Scoring;
using CommentIntelligence.Core.Storage;
using CommentIntelligence.Core.Text;
using CommentIntelligence.Core.Training;
using Microsoft.Extensions.DependencyInjection;

namespace CommentIntelligence.Core.DependencyInjection;

/// <summary>
/// Configuration surface for <see cref="ServiceCollectionExtensions.AddCommentIntelligence"/>.
/// Register one <see cref="LanguageTrainingSet"/> per supported language — there is no
/// automatic language detection; callers pass the culture explicitly to
/// <c>ICommentClassificationPipeline.Classify</c> (e.g. from the buyer's account locale
/// or the storefront's active language), and the matching per-language model is used.
/// </summary>
public sealed class CommentIntelligenceOptions
{
    public List<LanguageTrainingSet> Languages { get; } = new();

    /// <summary>Used when a comment's culture has no trained model of its own.</summary>
    public CultureInfo DefaultCulture { get; set; } = CultureInfo.GetCultureInfo("en");

    public VisibilityScoringOptions VisibilityScoringOptions { get; set; } = new();

    /// <summary>
    /// Directory where trained models are cached as JSON, keyed by a fingerprint of the
    /// training data that produced them. On startup, a language is only retrained if its
    /// training data changed since the last cache write — otherwise the cached model loads
    /// straight from disk. Set to null to disable caching and always retrain from scratch.
    /// </summary>
    public string? ModelCacheDirectory { get; set; }

    public void AddLanguage(CultureInfo culture, ITrainingDataProvider sentimentProvider, ITrainingDataProvider contentLabelProvider)
    {
        Languages.Add(new LanguageTrainingSet
        {
            Culture = culture,
            SentimentTrainingDataProvider = sentimentProvider,
            ContentLabelTrainingDataProvider = contentLabelProvider
        });
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full classification pipeline: text preprocessing, a per-culture model
    /// registry, the training service (used both at startup and for on-demand admin
    /// retraining), both Naive Bayes classifiers, the visibility scorer, and an in-memory
    /// comment store.
    ///
    /// NOTE: initial training still runs synchronously the first time the model registry
    /// is resolved (typically forced during app startup — see the Demo project's
    /// Program.cs for the recommended background-task pattern so it doesn't block the
    /// HTTP listener). Subsequent retraining via IModelTrainingService.RetrainAsync can be
    /// triggered anytime — by an admin endpoint, a scheduled job, or eventually a
    /// DB-driven "enough new comments accumulated" trigger — without restarting the app.
    /// </summary>
    public static IServiceCollection AddCommentIntelligence(this IServiceCollection services, Action<CommentIntelligenceOptions> configure)
    {
        var options = new CommentIntelligenceOptions();
        configure(options);

        if (options.Languages.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one language must be registered via CommentIntelligenceOptions.AddLanguage(...).");
        }

        services.AddSingleton<IStopWordProvider, EmbeddedStopWordProvider>();
        services.AddSingleton<ITextPreprocessor, DefaultTextPreprocessor>();
        services.AddSingleton(options.VisibilityScoringOptions);
        services.AddSingleton<IVisibilityScorer, VisibilityScorer>();

        services.AddSingleton<IModelRegistry>(_ => new ModelRegistry(options.DefaultCulture));
        services.AddSingleton<NaiveBayesModelCache>();

        services.AddSingleton<IModelTrainingService>(sp => new ModelTrainingService(
            options.Languages,
            sp.GetRequiredService<ITextPreprocessor>(),
            sp.GetRequiredService<IModelRegistry>(),
            sp.GetRequiredService<NaiveBayesModelCache>(),
            options.ModelCacheDirectory));

        services.AddSingleton(sp => new NaiveBayesPredictor(sp.GetRequiredService<ITextPreprocessor>()));
        services.AddSingleton<ISentimentClassifier, NaiveBayesSentimentClassifier>();
        services.AddSingleton<IContentLabelClassifier, NaiveBayesContentLabelClassifier>();

        services.AddSingleton<ICommentClassificationPipeline, CommentClassificationPipeline>();
        services.AddSingleton<IClassifiedCommentStore, InMemoryClassifiedCommentStore>();

        // Eagerly train all configured languages once a training service is requested.
        // The host app controls *when* this first resolution happens — see Program.cs,
        // which does it in a background task after the app starts listening rather than
        // blocking startup on it.
        services.AddSingleton(sp =>
        {
            var trainingService = sp.GetRequiredService<IModelTrainingService>();
            trainingService.TrainAllAsync().GetAwaiter().GetResult();
            return new ModelsTrainedMarker();
        });

        return services;
    }
}

/// <summary>
/// Empty marker type — resolving it from DI is what forces the eager TrainAllAsync call
/// above to run. Request it once at startup (see Program.cs) to warm the models before
/// the first real comment comes in.
/// </summary>
public sealed class ModelsTrainedMarker;