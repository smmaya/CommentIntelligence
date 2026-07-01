using System.Globalization;
using CommentIntelligence.Core.Classification;
using CommentIntelligence.Core.Classification.Persistence;
using CommentIntelligence.Core.Pipeline;
using CommentIntelligence.Core.Scoring;
using CommentIntelligence.Core.Storage;
using CommentIntelligence.Core.Text;
using CommentIntelligence.Core.Training;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CommentIntelligence.Core.DependencyInjection;

/// <summary>
/// Configuration surface for <see cref="ServiceCollectionExtensions.AddCommentIntelligence"/>.
/// </summary>
public sealed class CommentIntelligenceOptions
{
    internal List<LanguageTrainingSet> Languages { get; } = new();
    
    public UnsupportedLanguageBehaviour UnsupportedLanguageBehaviour { get; set; } =
        UnsupportedLanguageBehaviour.Reject;

    /// <summary>
    /// Fallback culture used when language detection is inconclusive.
    /// Defaults to English.
    /// </summary>
    public CultureInfo DefaultCulture { get; set; } = CultureInfo.GetCultureInfo("en");

    public VisibilityScoringOptions VisibilityScoringOptions { get; set; } = new();

    /// <summary>
    /// Directory where trained models are persisted as JSON after the first training run.
    /// On subsequent startups, a language only retrains if the training data has changed
    /// (SHA256 fingerprint comparison). Set to null to always retrain from scratch.
    /// </summary>
    public string? ModelCacheDirectory { get; set; }

    /// <summary>
    /// Registers a language using explicit <see cref="ITrainingDataProvider"/> instances —
    /// use when you need something other than a plain CSV file (blob storage, DB, composite, etc.).
    /// </summary>
    public void AddLanguage(CultureInfo culture, ITrainingDataProvider sentimentProvider, ITrainingDataProvider contentLabelProvider)
    {
        Languages.Add(new LanguageTrainingSet
        {
            Culture = culture,
            SentimentTrainingDataProvider = sentimentProvider,
            ContentLabelTrainingDataProvider = contentLabelProvider
        });
    }

    /// <summary>
    /// Convenience overload — registers a language from two CSV file paths.
    /// The most common case: training data lives on disk next to the app.
    /// </summary>
    public void AddLanguage(string twoLetterIsoLanguage, string sentimentCsvPath, string contentLabelCsvPath)
    {
        AddLanguage(
            CultureInfo.GetCultureInfo(twoLetterIsoLanguage),
            new FileTrainingDataProvider(sentimentCsvPath),
            new FileTrainingDataProvider(contentLabelCsvPath));
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full CommentIntelligence pipeline. The minimal host-app setup is:
    ///
    /// <code>
    /// builder.Services.AddCommentIntelligence(options =>
    /// {
    ///     options.ModelCacheDirectory = "/path/to/cache";
    ///     options.AddLanguage("en", "sentiment-en.csv", "content-label-en.csv");
    ///     options.AddLanguage("pl", "sentiment-pl.csv", "content-label-pl.csv");
    /// });
    ///
    /// app.MapCommentIntelligenceEndpoints(); // /admin/comment-intelligence/retrain
    /// </code>
    ///
    /// Then inject <see cref="ICommentClassificationPipeline"/> and <see cref="IClassifiedCommentStore"/>
    /// wherever needed. Language detection, model caching, startup training, and the retrain
    /// endpoint are all handled by the package.
    /// </summary>
    public static IServiceCollection AddCommentIntelligence(
        this IServiceCollection services,
        Action<CommentIntelligenceOptions> configure)
    {
        var options = new CommentIntelligenceOptions();
        configure(options);

        if (options.Languages.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one language must be registered via options.AddLanguage(...).");
        }

        // Text processing
        services.AddSingleton<IStopWordProvider, EmbeddedStopWordProvider>();
        services.TryAddSingleton<IStemmerProvider, NullStemmerProvider>(); 
        services.AddSingleton<ITextPreprocessor, DefaultTextPreprocessor>();

        // Scoring
        services.AddSingleton(options.VisibilityScoringOptions);
        services.AddSingleton<IVisibilityScorer, VisibilityScorer>();

        // Model registry — keyed by culture, hot-swappable on retrain
        services.AddSingleton<IModelRegistry>(_ => new ModelRegistry(options.DefaultCulture));
        services.AddSingleton<NaiveBayesModelCache>();

        // Training service — used at startup (via IHostedService) and on admin retrain
        services.AddSingleton<IModelTrainingService>(sp => new ModelTrainingService(
            options.Languages,
            sp.GetRequiredService<ITextPreprocessor>(),
            sp.GetRequiredService<IModelRegistry>(),
            sp.GetRequiredService<NaiveBayesModelCache>(),
            options.ModelCacheDirectory));

        // Language detector — auto-configured from the registered languages in the model
        // registry so it never returns a culture with no trained model behind it.
        // Register only if the host app hasn't already provided its own ILanguageDetector
        // (e.g. to always use the storefront's active language instead of auto-detecting).
        services.TryAddSingleton<ILanguageDetector>(sp => new LanguageDetectionAiDetector(
            sp.GetRequiredService<IModelRegistry>(),
            options.DefaultCulture));

        // Classifiers
        services.AddSingleton(sp => new NaiveBayesPredictor(sp.GetRequiredService<ITextPreprocessor>()));
        services.AddSingleton<ISentimentClassifier, NaiveBayesSentimentClassifier>();
        services.AddSingleton<IContentLabelClassifier, NaiveBayesContentLabelClassifier>();

        // Pipeline — the single thing host apps inject to classify a comment
        services.AddSingleton<ICommentClassificationPipeline>(sp => new CommentClassificationPipeline(
            sp.GetRequiredService<ISentimentClassifier>(),
            sp.GetRequiredService<IContentLabelClassifier>(),
            sp.GetRequiredService<IVisibilityScorer>(),
            sp.GetRequiredService<ILanguageDetector>(),
            sp.GetRequiredService<IModelRegistry>(),
            options.DefaultCulture,
            options.UnsupportedLanguageBehaviour));

        // Storage — in-memory default; replace with your own IClassifiedCommentStore for production
        services.AddSingleton<IClassifiedCommentStore, InMemoryClassifiedCommentStore>();

        // Hosted service — trains all configured languages at startup (cache-aware),
        // no manual marker resolution needed in Program.cs
        services.AddSingleton<ModelTrainingHostedService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ModelTrainingHostedService>());

        return services;
    }
}