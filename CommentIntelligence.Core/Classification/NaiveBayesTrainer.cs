using System.Globalization;
using CommentIntelligence.Core.Text;

namespace CommentIntelligence.Core.Classification;

/// <summary>
/// Trains a multinomial Naive Bayes model from a set of labeled examples.
/// The same trainer is reused for both the sentiment model (labels "1".."5")
/// and the content-label model (labels = ContentLabel names) — only the
/// training data differs.
/// </summary>
public sealed class NaiveBayesTrainer
{
    private readonly ITextPreprocessor _preprocessor;

    public NaiveBayesTrainer(ITextPreprocessor preprocessor)
    {
        _preprocessor = preprocessor;
    }

    public NaiveBayesModel Train(IEnumerable<TrainingExample> examples, CultureInfo? culture = null)
    {
        var model = new NaiveBayesModel();

        foreach (var example in examples)
        {
            var tokens = _preprocessor.Tokenize(example.Text, culture);
            if (tokens.Count == 0)
            {
                continue;
            }

            model.ClassDocumentCounts.TryGetValue(example.Label, out var docCount);
            model.ClassDocumentCounts[example.Label] = docCount + 1;

            if (!model.ClassWordCounts.TryGetValue(example.Label, out var wordCounts))
            {
                wordCounts = new Dictionary<string, int>();
                model.ClassWordCounts[example.Label] = wordCounts;
            }

            foreach (var token in tokens)
            {
                model.Vocabulary.Add(token);

                wordCounts.TryGetValue(token, out var count);
                wordCounts[token] = count + 1;

                model.ClassTotalWordCounts.TryGetValue(example.Label, out var total);
                model.ClassTotalWordCounts[example.Label] = total + 1;
            }

            if (!model.ClassCharacterNgramCounts.TryGetValue(example.Label, out var ngramCounts))
            {
                ngramCounts = new Dictionary<string, int>();
                model.ClassCharacterNgramCounts[example.Label] = ngramCounts;
            }

            foreach (var ngram in CharacterNgrams(tokens))
            {
                model.CharacterNgramVocabulary.Add(ngram);
                ngramCounts.TryGetValue(ngram, out var count);
                ngramCounts[ngram] = count + 1;
                model.ClassTotalCharacterNgramCounts.TryGetValue(example.Label, out var total);
                model.ClassTotalCharacterNgramCounts[example.Label] = total + 1;
            }
        }

        return model;
    }

    public NaiveBayesModel TrainBinary(
        IEnumerable<TrainingExample> examples,
        string positiveLabel,
        CultureInfo? culture = null)
    {
        return Train(examples.Select(example => new TrainingExample
        {
            Text = example.Text,
            Label = string.Equals(example.Label, positiveLabel, StringComparison.OrdinalIgnoreCase)
                ? "positive"
                : "negative"
        }), culture);
    }

    internal static IEnumerable<string> CharacterNgrams(IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            var value = $"^{token}$";
            for (var length = 3; length <= 5; length++)
            {
                for (var index = 0; index <= value.Length - length; index++)
                    yield return value.Substring(index, length);
            }
        }
    }
}
