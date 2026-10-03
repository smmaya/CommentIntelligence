using System.Globalization;
using CommentIntelligence.Core.Classification;
using CommentIntelligence.Core.Classification.Persistence.ModelCache;
using CommentIntelligence.Core.Evaluation;
using CommentIntelligence.Core.Contracts;
using CommentIntelligence.Core.Pipeline;
using CommentIntelligence.Core.Scoring;
using CommentIntelligence.Core.Text;
using CommentIntelligence.Core.Training;
using Xunit;

namespace CommentIntelligence.Core.Tests;

public sealed class ClassificationTests
{
    [Fact]
    public void Predictor_does_not_classify_text_without_usable_tokens()
    {
        var preprocessor = new DefaultTextPreprocessor(
            new TestStopWords("the", "and", "a"),
            new NullStemmerProvider());
        var predictor = new NaiveBayesPredictor(preprocessor);
        var model = new NaiveBayesTrainer(preprocessor).Train(
        [
            new TrainingExample { Text = "solid wood table", Label = "5" },
            new TrainingExample { Text = "poor quality table", Label = "1" }
        ], CultureInfo.GetCultureInfo("en"));

        var result = predictor.Predict("the and a", model, CultureInfo.GetCultureInfo("en"));

        Assert.False(result.HasUsableTokens);
        Assert.Equal(0, result.Confidence);
        Assert.Empty(result.PredictedLabel);
    }

    [Fact]
    public void Predictor_handles_product_word_variants_without_exact_training_sentence()
    {
        var preprocessor = new DefaultTextPreprocessor();
        var predictor = new NaiveBayesPredictor(preprocessor);
        var model = new NaiveBayesTrainer(preprocessor).Train(
        [
            new TrainingExample { Text = "Super sofa wygodna miękka polecam", Label = "5" },
            new TrainingExample { Text = "Sofa świetna wygodna", Label = "5" },
            new TrainingExample { Text = "Sofa niewygodna tandetna", Label = "1" }
        ], CultureInfo.GetCultureInfo("pl"));

        var result = predictor.Predict(
            "Super sofka, wygodna i miękka, polecam.",
            model,
            CultureInfo.GetCultureInfo("pl"));

        Assert.Equal("5", result.PredictedLabel);
    }

    [Fact]
    public void Content_predictor_does_not_turn_short_positive_opinion_into_low_quality()
    {
        var preprocessor = new DefaultTextPreprocessor();
        var predictor = new NaiveBayesPredictor(preprocessor);
        var model = new NaiveBayesTrainer(preprocessor).Train(
        [
            new TrainingExample { Text = "Sofa jest wygodna miękka polecam", Label = nameof(ContentLabel.Emotional) },
            new TrainingExample { Text = "Bardzo wygodna sofa jestem zachwycony", Label = nameof(ContentLabel.Emotional) },
            new TrainingExample { Text = "Nie warto wspominać o tym produkcie", Label = nameof(ContentLabel.LowQuality) },
            new TrainingExample { Text = "Produkt nijaki bez żadnych informacji", Label = nameof(ContentLabel.LowQuality) }
        ], CultureInfo.GetCultureInfo("pl"));

        var result = predictor.Predict(
            "Super sofka, wygodna i miękka, polecam.",
            model,
            CultureInfo.GetCultureInfo("pl"));

        Assert.Equal(nameof(ContentLabel.Emotional), result.PredictedLabel);
    }

    [Fact]
    public void Content_classifier_exposes_independent_label_probabilities()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var preprocessor = new DefaultTextPreprocessor();
        var trainer = new NaiveBayesTrainer(preprocessor);
        var examples = new[]
        {
            new TrainingExample { Text = "large easy to clean table", Label = nameof(ContentLabel.Informative) },
            new TrainingExample { Text = "useful practical advice", Label = nameof(ContentLabel.Helpful) },
            new TrainingExample { Text = "I love this beautiful table", Label = nameof(ContentLabel.Emotional) },
            new TrainingExample { Text = "buy this table", Label = nameof(ContentLabel.Tendentious) },
            new TrainingExample { Text = "awful insulting product", Label = nameof(ContentLabel.Hateful) },
            new TrainingExample { Text = "no details at all", Label = nameof(ContentLabel.LowQuality) }
        };
        var registry = new ModelRegistry(culture);
        var models = Enum.GetValues<ContentLabel>()
            .Where(label => label != ContentLabel.Unknown)
            .ToDictionary(label => label, label => trainer.TrainBinary(examples, label.ToString(), culture));
        registry.Set(culture, new ClassifierModelSet
        {
            SentimentModel = new NaiveBayesModel(),
            ContentLabelModel = models[ContentLabel.Informative],
            ContentLabelModels = models
        });

        var result = new NaiveBayesContentLabelClassifier(registry, new NaiveBayesPredictor(preprocessor))
            .ClassifyContent("large useful table", culture);

        Assert.Contains(nameof(ContentLabel.Informative), result.ClassProbabilities.Keys);
        Assert.Contains(nameof(ContentLabel.Helpful), result.ClassProbabilities.Keys);
        Assert.NotEqual(result.ClassProbabilities[nameof(ContentLabel.Informative)],
            result.ClassProbabilities[nameof(ContentLabel.Helpful)]);
    }

    [Fact]
    public void Scorer_rejects_invalid_configuration()
    {
        var options = new VisibilityScoringOptions { RecencyHalfLifeDays = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new VisibilityScorer(options));
    }

    [Fact]
    public void Scorer_caps_hateful_and_tendentious_visibility()
    {
        var scorer = new VisibilityScorer();

        var hateful = scorer.Score(ContentLabel.Hateful, 1, 1, 3, DateTimeOffset.UtcNow);
        var tendentious = scorer.Score(ContentLabel.Tendentious, 1, 1, 5, DateTimeOffset.UtcNow);

        Assert.True(hateful <= 0.1);
        Assert.True(tendentious <= 0.25);
    }

    [Fact]
    public void Cache_fingerprint_changes_when_configuration_changes()
    {
        var examples = new[]
        {
            new TrainingExample { Text = "solid table", Label = "5" }
        };

        var first = NaiveBayesModelCache.ComputeFingerprint(examples, "preprocessor-v1");
        var second = NaiveBayesModelCache.ComputeFingerprint(examples, "preprocessor-v2");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Cache_round_trip_preserves_versioned_metadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"comment-intelligence-{Guid.NewGuid():N}.json");
        try
        {
            var envelope = new CachedModelEnvelope
            {
                Culture = "en",
                ModelKind = "sentiment",
                ConfigurationFingerprint = "config-v2",
                TrainingDataFingerprint = "data-v2",
                Model = new NaiveBayesModel()
            };

            var cache = new NaiveBayesModelCache();
            await cache.SaveAsync(path, envelope);
            var loaded = await cache.TryLoadAsync(path);

            Assert.NotNull(loaded);
            Assert.Equal(CachedModelEnvelope.CurrentSchemaVersion, loaded!.SchemaVersion);
            Assert.Equal("config-v2", loaded.ConfigurationFingerprint);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task Cache_rejects_old_schema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"comment-intelligence-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, """{"SchemaVersion":1}""");
            Assert.Null(await new NaiveBayesModelCache().TryLoadAsync(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Pipeline_rejects_unsupported_language_before_classification()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier(),
            new FixedContentClassifier(),
            new VisibilityScorer(),
            new FixedLanguageDetector(CultureInfo.GetCultureInfo("fr")),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject);

        var result = pipeline.Classify("Bonjour");

        Assert.False(result.IsSupported);
        Assert.Equal("fr", result.UnsupportedLanguageCode);
        Assert.Equal(ContentLabel.Unknown, result.ContentLabel);
    }

    [Theory]
    [InlineData("Quelle heure est-il?", "fr")]
    [InlineData("Quelle heure est-il ?", "fr")]
    public void Detector_handles_punctuation_spacing_in_short_text(string text, string expectedLanguage)
    {
        var english = CultureInfo.GetCultureInfo("en");
        var polish = CultureInfo.GetCultureInfo("pl");
        var registry = new ModelRegistry(english);
        registry.Set(english, EmptyModelSet());
        registry.Set(polish, EmptyModelSet());

        var detected = new LanguageDetectionAiDetector(registry, english).Detect(text);

        Assert.NotNull(detected);
        Assert.Equal(expectedLanguage, detected!.TwoLetterISOLanguageName);
    }

    [Fact]
    public void Pipeline_returns_zeroed_result_for_tokenless_classification()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier { Result = new() { PredictedLabel = "", Confidence = 0, HasUsableTokens = false } },
            new FixedContentClassifier { Result = new() { PredictedLabel = "", Confidence = 0, HasUsableTokens = false } },
            new VisibilityScorer(),
            new FixedLanguageDetector(null),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject);

        var result = pipeline.Classify("...");

        Assert.Equal(0, result.PredictedStars);
        Assert.Equal(ContentLabel.Unknown, result.ContentLabel);
        Assert.Equal(0, result.VisibilityScore);
    }

    [Fact]
    public void Pipeline_abstains_on_short_text_without_product_context()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier
            {
                Result = new()
                {
                    PredictedLabel = "4",
                    Confidence = 1,
                    HasUsableTokens = true
                }
            },
            new FixedContentClassifier
            {
                Result = new()
                {
                    PredictedLabel = nameof(ContentLabel.Emotional),
                    Confidence = 1,
                    HasUsableTokens = true
                }
            },
            new VisibilityScorer(),
            new FixedLanguageDetector(null),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject);

        var result = pipeline.Classify("What time is it ?");

        Assert.True(result.IsLowInformation);
        Assert.Equal(0, result.PredictedStars);
        Assert.Equal(ContentLabel.Unknown, result.ContentLabel);
        Assert.Equal(0, result.VisibilityScore);
    }

    [Fact]
    public void Pipeline_abstains_when_text_has_no_configured_domain_term()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var relevanceOptions = new DomainRelevanceOptions();
        relevanceOptions.AddLanguage("en", ["sofa", "table", "chair"]);
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier(),
            new FixedContentClassifier(),
            new VisibilityScorer(),
            new FixedLanguageDetector(null),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject,
            domainRelevanceOptions: relevanceOptions);

        var result = pipeline.Classify("I am not sure about this product.");

        Assert.True(result.IsLowInformation);
        Assert.Equal(ContentLabel.Unknown, result.ContentLabel);
        Assert.Equal(0, result.VisibilityScore);
    }

    [Fact]
    public void Pipeline_blocks_one_word_abuse_like_any_other_insufficient_input()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var safetyOptions = CreateEnglishSafetyOptions();
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier(),
            new FixedContentClassifier(),
            new VisibilityScorer(),
            new FixedLanguageDetector(null),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject,
            safetyOptions: safetyOptions);

        var result = pipeline.Classify("Fuck");

        Assert.True(result.IsLowInformation);
        Assert.Equal(0, result.PredictedStars);
        Assert.Equal(ContentLabel.Unknown, result.ContentLabel);
        Assert.Equal(0, result.VisibilityScore);
    }

    [Fact]
    public void Pipeline_blocks_abuse_without_domain_context()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var relevanceOptions = new DomainRelevanceOptions();
        relevanceOptions.AddLanguage("en", ["sofa", "table", "chair"]);
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier(),
            new FixedContentClassifier(),
            new VisibilityScorer(),
            new FixedLanguageDetector(null),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject,
            safetyOptions: CreateEnglishSafetyOptions(),
            domainRelevanceOptions: relevanceOptions);

        var result = pipeline.Classify("Shit fuck crap");

        Assert.True(result.IsLowInformation);
        Assert.Equal(0, result.PredictedStars);
        Assert.Equal(ContentLabel.Unknown, result.ContentLabel);
        Assert.Equal(0, result.VisibilityScore);
    }

    [Fact]
    public void Pipeline_abstains_when_prediction_confidence_is_below_threshold()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier
            {
                Result = new()
                {
                    PredictedLabel = "5",
                    Confidence = 0.2,
                    ClassProbabilities = new Dictionary<string, double> { ["5"] = 0.2, ["3"] = 0.8 }
                }
            },
            new FixedContentClassifier(),
            new VisibilityScorer(),
            new FixedLanguageDetector(null),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject);

        var result = pipeline.Classify("uncertain");

        Assert.Equal(0, result.PredictedStars);
        Assert.Equal(ContentLabel.Unknown, result.ContentLabel);
    }

    [Fact]
    public void Pipeline_overrides_tendentious_prediction_for_explicit_english_abuse()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier(),
            new FixedContentClassifier
            {
                Result = new()
                {
                    PredictedLabel = nameof(ContentLabel.Tendentious),
                    Confidence = 0.9,
                    ClassProbabilities = new Dictionary<string, double>
                    {
                        [nameof(ContentLabel.Tendentious)] = 0.9,
                        [nameof(ContentLabel.Hateful)] = 0.1
                    }
                }
            },
            new VisibilityScorer(),
            new FixedLanguageDetector(null),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject,
            safetyOptions: CreateEnglishSafetyOptions());

        var result = pipeline.Classify("Fuck you bastards! Don't buy furniture here!");

        Assert.Equal(ContentLabel.Hateful, result.ContentLabel);
        Assert.Equal(1, result.PredictedStars);
        Assert.True(result.VisibilityScore <= 0.1);
    }

    [Fact]
    public void Pipeline_forces_one_star_for_explicit_abuse_without_negative_phrase()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier
            {
                Result = new()
                {
                    PredictedLabel = "4",
                    Confidence = 0.99
                }
            },
            new FixedContentClassifier(),
            new VisibilityScorer(),
            new FixedLanguageDetector(null),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject,
            safetyOptions: CreateEnglishSafetyOptions());

        var result = pipeline.Classify("Fuck this sofa shit!");

        Assert.Equal(1, result.PredictedStars);
        Assert.Equal(ContentLabel.Hateful, result.ContentLabel);
        Assert.True(result.VisibilityScore <= 0.1);
    }

    [Fact]
    public void Pipeline_returns_secondary_content_labels_above_evidence_threshold()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var registry = new ModelRegistry(culture);
        registry.Set(culture, EmptyModelSet());
        var pipeline = new CommentClassificationPipeline(
            new FixedSentimentClassifier(),
            new FixedContentClassifier
            {
                Result = new()
                {
                    PredictedLabel = nameof(ContentLabel.Informative),
                    Confidence = 0.8,
                    ClassProbabilities = new Dictionary<string, double>
                    {
                        [nameof(ContentLabel.Informative)] = 0.8,
                        [nameof(ContentLabel.Helpful)] = 0.12,
                        [nameof(ContentLabel.Emotional)] = 0.04
                    }
                }
            },
            new VisibilityScorer(),
            new FixedLanguageDetector(null),
            registry,
            culture,
            UnsupportedLanguageBehaviour.Reject);

        var result = pipeline.Classify("The table is large and easy to clean.");

        Assert.Equal(
            [ContentLabel.Informative, ContentLabel.Helpful],
            result.ContentLabels);
        Assert.DoesNotContain(ContentLabel.Emotional, result.ContentLabels);
    }

    [Fact]
    public void Language_detector_does_not_return_empty_language_for_short_english_comment()
    {
        var registry = new ModelRegistry(CultureInfo.GetCultureInfo("en"));
        registry.Set(CultureInfo.GetCultureInfo("en"), EmptyModelSet());
        registry.Set(CultureInfo.GetCultureInfo("pl"), EmptyModelSet());
        var detector = new LanguageDetectionAiDetector(registry, CultureInfo.GetCultureInfo("en"));

        var detected = detector.Detect("What a piece of crap, don’t buy furniture here!");

        Assert.NotNull(detected);
        Assert.Equal("en", detected!.TwoLetterISOLanguageName);
    }

    [Fact]
    public void Language_detector_uses_known_polish_marker_for_short_comment()
    {
        var registry = new ModelRegistry(CultureInfo.GetCultureInfo("en"));
        registry.Set(CultureInfo.GetCultureInfo("en"), EmptyModelSet());
        registry.Set(CultureInfo.GetCultureInfo("pl"), EmptyModelSet());
        var detector = new LanguageDetectionAiDetector(registry, CultureInfo.GetCultureInfo("en"));

        var detected = detector.Detect("Super sofa, polecam.");

        Assert.NotNull(detected);
        Assert.Equal("pl", detected!.TwoLetterISOLanguageName);
    }

    [Fact]
    public async Task Csv_provider_reads_quoted_multiline_text()
    {
        var path = Path.Combine(Path.GetTempPath(), $"comment-intelligence-{Guid.NewGuid():N}.csv");
        try
        {
            await File.WriteAllTextAsync(path, "text,label\n\"Solid table\nwith clear instructions\",Helpful\n");
            var examples = await new FileTrainingDataProvider(path).LoadAsync();

            Assert.Single(examples);
            Assert.Contains("with clear instructions", examples[0].Text);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Evaluator_reports_accuracy_macro_f1_and_confusion()
    {
        var examples = new[]
        {
            new EvaluationExample("a", "good", "positive"),
            new EvaluationExample("b", "bad", "negative"),
            new EvaluationExample("c", "mixed", "negative")
        };

        var report = ClassificationEvaluator.Evaluate(
            examples,
            text => text == "bad" ? "negative" : "positive");

        Assert.Equal(2d / 3d, report.Accuracy, 8);
        Assert.Equal(1, report.Metrics["positive"].Support);
        Assert.Equal(1, report.ConfusionMatrix["negative"]["negative"]);
        Assert.Equal(1, report.ConfusionMatrix["negative"]["positive"]);
    }

    [Fact]
    public void Grouped_split_keeps_groups_together()
    {
        var examples = new[]
        {
            new EvaluationExample("template-a", "a1", "1"),
            new EvaluationExample("template-a", "a2", "1"),
            new EvaluationExample("template-b", "b1", "2"),
            new EvaluationExample("template-c", "c1", "3")
        };

        var (train, validation) = ClassificationEvaluator.SplitByGroup(examples, 0.34);

        Assert.Empty(train.Select(row => row.GroupId).Intersect(validation.Select(row => row.GroupId)));
        Assert.Equal(examples.Length, train.Count + validation.Count);
    }

    private sealed class TestStopWords(params string[] words) : IStopWordProvider
    {
        private readonly HashSet<string> _words = words.ToHashSet(StringComparer.Ordinal);
        public IReadOnlySet<string> GetStopWords(CultureInfo culture) => _words;
    }

    private sealed class FixedLanguageDetector(CultureInfo? culture) : ILanguageDetector
    {
        public CultureInfo? Detect(string text) => culture;
    }

    private sealed class FixedSentimentClassifier : ISentimentClassifier
    {
        public ClassificationResult Result { get; init; } = new() { PredictedLabel = "4", Confidence = 0.9 };
        public ClassificationResult ClassifyStars(string text, CultureInfo? culture = null) => Result;
    }

    private sealed class FixedContentClassifier : IContentLabelClassifier
    {
        public ClassificationResult Result { get; init; } = new() { PredictedLabel = nameof(ContentLabel.Helpful), Confidence = 0.9 };
        public ClassificationResult ClassifyContent(string text, CultureInfo? culture = null) => Result;
    }

    private static ClassifierModelSet EmptyModelSet() => new()
    {
        SentimentModel = new NaiveBayesModel(),
        ContentLabelModel = new NaiveBayesModel()
    };

    private static LanguageSafetyOptions CreateEnglishSafetyOptions()
    {
        var options = new LanguageSafetyOptions();
        options.AddLanguage(
            "en",
            ["fuck", "bastards"],
            ["don't buy"]);
        return options;
    }
}
