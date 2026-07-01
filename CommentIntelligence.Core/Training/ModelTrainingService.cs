using System.Globalization;
using CommentIntelligence.Core.Classification;
using CommentIntelligence.Core.Classification.Persistence;
using CommentIntelligence.Core.Text;

namespace CommentIntelligence.Core.Training;

public sealed class ModelTrainingService : IModelTrainingService
{
    private readonly IReadOnlyList<LanguageTrainingSet> _languageSets;
    private readonly ITextPreprocessor _preprocessor;
    private readonly IModelRegistry _registry;
    private readonly NaiveBayesModelCache _cache;
    private readonly string? _cacheDirectory;

    public ModelTrainingService(
        IReadOnlyList<LanguageTrainingSet> languageSets,
        ITextPreprocessor preprocessor,
        IModelRegistry registry,
        NaiveBayesModelCache cache,
        string? cacheDirectory)
    {
        _languageSets = languageSets;
        _preprocessor = preprocessor;
        _registry = registry;
        _cache = cache;
        _cacheDirectory = cacheDirectory;
    }

    public async Task TrainAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var languageSet in _languageSets)
        {
            await TrainLanguageAsync(languageSet, forceRetrain: false, cancellationToken);
        }
    }

    public async Task RetrainAsync(CultureInfo culture, CancellationToken cancellationToken = default)
    {
        var languageSet = _languageSets.FirstOrDefault(l =>
            l.Culture.TwoLetterISOLanguageName.Equals(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));

        if (languageSet is null)
        {
            throw new InvalidOperationException(
                $"No LanguageTrainingSet configured for culture '{culture.TwoLetterISOLanguageName}'. " +
                "Register one via CommentIntelligenceOptions.Languages before retraining it.");
        }

        await TrainLanguageAsync(languageSet, forceRetrain: true, cancellationToken);
    }

    public async Task RetrainAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var languageSet in _languageSets)
        {
            await TrainLanguageAsync(languageSet, forceRetrain: true, cancellationToken);
        }
    }

    private async Task TrainLanguageAsync(LanguageTrainingSet languageSet, bool forceRetrain, CancellationToken cancellationToken)
    {
        var sentimentModel = await TrainOrLoadAsync(
            languageSet.Culture, "sentiment", languageSet.SentimentTrainingDataProvider, forceRetrain, cancellationToken);

        var contentModel = await TrainOrLoadAsync(
            languageSet.Culture, "content-label", languageSet.ContentLabelTrainingDataProvider, forceRetrain, cancellationToken);

        _registry.Set(languageSet.Culture, new ClassifierModelSet
        {
            SentimentModel = sentimentModel,
            ContentLabelModel = contentModel
        });
    }

    private async Task<NaiveBayesModel> TrainOrLoadAsync(
        CultureInfo culture,
        string modelKind,
        ITrainingDataProvider provider,
        bool forceRetrain,
        CancellationToken cancellationToken)
    {
        var examples = await provider.LoadAsync(cancellationToken);
        var fingerprint = NaiveBayesModelCache.ComputeFingerprint(examples);
        var cachePath = GetCachePath(culture, modelKind);

        if (!forceRetrain && cachePath is not null)
        {
            var cached = await _cache.TryLoadAsync(cachePath, cancellationToken);
            if (cached is not null && cached.TrainingDataFingerprint == fingerprint)
            {
                return cached.Model;
            }
        }

        var trainer = new NaiveBayesTrainer(_preprocessor);
        var model = trainer.Train(examples, culture);

        if (cachePath is not null)
        {
            await _cache.SaveAsync(cachePath, new CachedModelEnvelope
            {
                TrainingDataFingerprint = fingerprint,
                Model = model
            }, cancellationToken);
        }

        return model;
    }

    private string? GetCachePath(CultureInfo culture, string modelKind)
    {
        if (_cacheDirectory is null)
        {
            return null;
        }

        var languageCode = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        return Path.Combine(_cacheDirectory, $"{modelKind}-{languageCode}.json");
    }
}