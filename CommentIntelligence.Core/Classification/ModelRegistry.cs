using System.Collections.Concurrent;
using System.Globalization;

namespace CommentIntelligence.Core.Classification;

public sealed class ModelRegistry : IModelRegistry
{
    private readonly ConcurrentDictionary<string, ClassifierModelSet> _modelsByCulture = new();
    private readonly CultureInfo _defaultCulture;

    public ModelRegistry(CultureInfo defaultCulture)
    {
        _defaultCulture = defaultCulture;
    }

    public IReadOnlyCollection<CultureInfo> SupportedCultures =>
        _modelsByCulture.Keys.Select(CultureInfo.GetCultureInfo).ToList();

    public ClassifierModelSet Resolve(CultureInfo? culture)
    {
        var key = CultureKey(culture ?? _defaultCulture);

        if (_modelsByCulture.TryGetValue(key, out var models))
        {
            return models;
        }

        // No model trained for this language yet — fall back to the default culture
        // rather than throwing, so an untrained language degrades gracefully instead
        // of breaking comment submission entirely.
        var defaultKey = CultureKey(_defaultCulture);
        if (_modelsByCulture.TryGetValue(defaultKey, out var defaultModels))
        {
            return defaultModels;
        }

        throw new InvalidOperationException(
            $"No trained model available for culture '{key}' and no default culture '{defaultKey}' model is registered either.");
    }

    public void Set(CultureInfo culture, ClassifierModelSet models)
    {
        _modelsByCulture[CultureKey(culture)] = models;
    }

    private static string CultureKey(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.ToLowerInvariant();
}