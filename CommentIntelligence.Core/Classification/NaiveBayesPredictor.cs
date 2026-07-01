using System.Globalization;
using CommentIntelligence.Core.Models;
using CommentIntelligence.Core.Text;

namespace CommentIntelligence.Core.Classification;

/// <summary>
/// Predicts a label for a piece of text against a trained <see cref="NaiveBayesModel"/>.
/// Uses Laplace (add-one) smoothing so unseen words never zero out a class probability,
/// and works in log-space with a numerically stable softmax normalization at the end
/// so confidences are well-behaved even with large vocabularies.
/// </summary>
public sealed class NaiveBayesPredictor
{
    private readonly ITextPreprocessor _preprocessor;
    private const double SmoothingFactor = 1.0;

    public NaiveBayesPredictor(ITextPreprocessor preprocessor)
    {
        _preprocessor = preprocessor;
    }

    public ClassificationResult Predict(string text, NaiveBayesModel model, CultureInfo? culture = null)
    {
        if (model.TotalDocuments == 0)
        {
            return new ClassificationResult
            {
                PredictedLabel = string.Empty,
                Confidence = 0,
                ClassProbabilities = new Dictionary<string, double>()
            };
        }

        var tokens = _preprocessor.Tokenize(text, culture);
        var vocabularySize = Math.Max(model.Vocabulary.Count, 1);
        var totalDocuments = model.TotalDocuments;

        var logScores = new Dictionary<string, double>();

        foreach (var label in model.ClassDocumentCounts.Keys)
        {
            var prior = (double)model.ClassDocumentCounts[label] / totalDocuments;
            var logScore = Math.Log(prior);

            model.ClassWordCounts.TryGetValue(label, out var wordCounts);
            model.ClassTotalWordCounts.TryGetValue(label, out var totalWordsInClass);
            var denominator = totalWordsInClass + SmoothingFactor * vocabularySize;

            foreach (var token in tokens)
            {
                var count = 0;
                wordCounts?.TryGetValue(token, out count);
                var likelihood = (count + SmoothingFactor) / denominator;
                logScore += Math.Log(likelihood);
            }

            logScores[label] = logScore;
        }

        // Numerically stable softmax: subtract the max log-score before exponentiating.
        var maxLog = logScores.Values.Max();
        var expScores = logScores.ToDictionary(kv => kv.Key, kv => Math.Exp(kv.Value - maxLog));
        var sumExp = expScores.Values.Sum();
        var probabilities = expScores.ToDictionary(kv => kv.Key, kv => kv.Value / sumExp);

        var best = probabilities.OrderByDescending(kv => kv.Value).First();

        return new ClassificationResult
        {
            PredictedLabel = best.Key,
            Confidence = best.Value,
            ClassProbabilities = probabilities
        };
    }
}
