using System.Globalization;
using CommentIntelligence.Core.Classification.Persistence.ModelCache;
using CommentIntelligence.Core.Text;

namespace CommentIntelligence.Core.Training;

public sealed class ModelTrainingService : IModelTrainingService
{
    private readonly IReadOnlyList<LanguageTrainingSet> _languageSets;
    private readonly ITextPreprocessor _preprocessor;
    private readonly IModelRegistry _registry;
    private readonly NaiveBayesModelCache _cache;
    private readonly string? _cacheDirectory;
    private readonly SemaphoreSlim _trainingGate = new(1, 1);

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
        await _trainingGate.WaitAsync(cancellationToken);
        try
        {
            var sentimentModel = await TrainOrLoadAsync(
                languageSet.Culture, "sentiment", languageSet.SentimentTrainingDataProvider, forceRetrain, cancellationToken);

            var contentExamples = await languageSet.ContentLabelTrainingDataProvider.LoadAsync(cancellationToken);
            ValidateExamples(contentExamples, "content-label", languageSet.Culture);
            var contentModels = new Dictionary<ContentLabel, NaiveBayesModel>();
            foreach (var label in Enum.GetValues<ContentLabel>().Where(label => label != ContentLabel.Unknown))
            {
                contentModels[label] = await TrainOrLoadAsync(
                    languageSet.Culture, $"content-label-{label}", contentExamples, forceRetrain, cancellationToken);
            }

            _registry.Set(languageSet.Culture, new ClassifierModelSet
            {
                SentimentModel = sentimentModel,
                ContentLabelModel = contentModels[ContentLabel.Informative],
                ContentLabelModels = contentModels
            });
        }
        finally
        {
            _trainingGate.Release();
        }
    }

    private async Task<NaiveBayesModel> TrainOrLoadAsync(
        CultureInfo culture,
        string modelKind,
        ITrainingDataProvider provider,
        bool forceRetrain,
        CancellationToken cancellationToken)
    {
        var examples = await provider.LoadAsync(cancellationToken);
        return await TrainOrLoadAsync(culture, modelKind, examples, forceRetrain, cancellationToken);
    }

    private async Task<NaiveBayesModel> TrainOrLoadAsync(
        CultureInfo culture,
        string modelKind,
        IReadOnlyList<TrainingExample> examples,
        bool forceRetrain,
        CancellationToken cancellationToken)
    {
        ValidateExamples(examples, modelKind, culture);
        var configurationFingerprint = $"{culture.TwoLetterISOLanguageName.ToLowerInvariant()}|{modelKind}|{typeof(DefaultTextPreprocessor).FullName}|preprocessor-v2|naive-bayes-v4-independent-content-labels|laplace-1";
        var fingerprint = NaiveBayesModelCache.ComputeFingerprint(examples, configurationFingerprint);
        var cachePath = GetCachePath(culture, modelKind);

        if (!forceRetrain && cachePath is not null)
        {
            var cached = await _cache.TryLoadAsync(cachePath, cancellationToken);
            if (cached is not null &&
                cached.TrainingDataFingerprint == fingerprint &&
                cached.ConfigurationFingerprint == configurationFingerprint &&
                cached.Culture.Equals(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase) &&
                cached.ModelKind.Equals(modelKind, StringComparison.OrdinalIgnoreCase))
            {
                return cached.Model;
            }
        }

        var trainer = new NaiveBayesTrainer(_preprocessor);
        var model = modelKind.StartsWith("content-label-", StringComparison.Ordinal)
            ? trainer.TrainBinary(examples, modelKind["content-label-".Length..], culture)
            : trainer.Train(examples, culture);
        ValidateTrainedModel(model, modelKind, culture);

        if (cachePath is not null)
        {
            await _cache.SaveAsync(cachePath, new CachedModelEnvelope
            {
                Culture = culture.TwoLetterISOLanguageName,
                ModelKind = modelKind,
                ConfigurationFingerprint = configurationFingerprint,
                TrainingDataFingerprint = fingerprint,
                Model = model
            }, cancellationToken);
        }

        return model;
    }

    private static void ValidateExamples(
        IReadOnlyList<TrainingExample> examples,
        string modelKind,
        CultureInfo culture)
    {
        if (examples.Count == 0)
            throw new InvalidDataException($"No training examples were loaded for {modelKind}/{culture.TwoLetterISOLanguageName}.");

        var labels = examples.Select(example => example.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (modelKind == "sentiment")
        {
            var invalid = labels.Where(label => !int.TryParse(label, out var stars) || stars is < 1 or > 5).ToArray();
            if (invalid.Length > 0)
                throw new InvalidDataException($"Invalid sentiment labels for {culture.TwoLetterISOLanguageName}: {string.Join(", ", invalid)}.");

            var missing = Enumerable.Range(1, 5).Select(value => value.ToString())
                .Where(label => !labels.Contains(label)).ToArray();
            if (missing.Length > 0)
                throw new InvalidDataException($"Missing sentiment labels for {culture.TwoLetterISOLanguageName}: {string.Join(", ", missing)}.");
        }

        else if (modelKind == "content-label")
        {
            var validLabels = Enum.GetNames<ContentLabel>()
                .Where(label => label != nameof(ContentLabel.Unknown))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalid = labels.Where(label => !validLabels.Contains(label)).ToArray();
            if (invalid.Length > 0)
                throw new InvalidDataException($"Invalid content labels for {culture.TwoLetterISOLanguageName}: {string.Join(", ", invalid)}.");

            var missing = validLabels.Where(label => !labels.Contains(label)).ToArray();
            if (missing.Length > 0)
                throw new InvalidDataException($"Missing content labels for {culture.TwoLetterISOLanguageName}: {string.Join(", ", missing)}.");
        }
    }

    private static void ValidateTrainedModel(
        NaiveBayesModel model,
        string modelKind,
        CultureInfo culture)
    {
        if (model.TotalDocuments == 0)
            throw new InvalidDataException($"Training data for {modelKind}/{culture.TwoLetterISOLanguageName} contains no usable tokens.");

        if (modelKind == "sentiment")
        {
            var missing = Enumerable.Range(1, 5).Select(value => value.ToString())
                .Where(label => !model.ClassDocumentCounts.ContainsKey(label)).ToArray();
            if (missing.Length > 0)
                throw new InvalidDataException($"Training data for {culture.TwoLetterISOLanguageName} has no usable examples for sentiment labels: {string.Join(", ", missing)}.");
        }
        else if (modelKind.StartsWith("content-label-", StringComparison.Ordinal))
        {
            var missing = new[] { "positive", "negative" }
                .Where(label => !model.ClassDocumentCounts.ContainsKey(label)).ToArray();
            if (missing.Length > 0)
                throw new InvalidDataException($"Training data for {culture.TwoLetterISOLanguageName} has no usable examples for binary content label '{modelKind["content-label-".Length..]}': {string.Join(", ", missing)}.");
        }
        else if (modelKind == "content-label")
        {
            var missing = Enum.GetNames<ContentLabel>()
                .Where(label => label != nameof(ContentLabel.Unknown))
                .Where(label => !model.ClassDocumentCounts.ContainsKey(label))
                .ToArray();
            if (missing.Length > 0)
                throw new InvalidDataException($"Training data for {culture.TwoLetterISOLanguageName} has no usable examples for content labels: {string.Join(", ", missing)}.");
        }
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