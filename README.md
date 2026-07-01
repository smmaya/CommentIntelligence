# CommentIntelligence

A .NET 10 library that classifies e-commerce and social comments using a multinomial
Naive Bayes pipeline. Derives star ratings and content-quality labels directly from
review text — no user-submitted stars, no peer voting required.

## Core design decision

**Stars are not submitted by users.** They are predicted from the comment text by the
sentiment classifier. Separately, a content-label classifier determines what *kind* of
comment it is. These two axes are independent on purpose:

- A 1-star review can be `Informative` and rank high — useful for a buying decision
  even though it is negative.
- A 5-star review can be `LowQuality` ("nice!!") and rank low — adds nothing.

The `VisibilityScore` (0..1) combines both axes plus recency decay, and is what
comment lists sort by when "most useful" is selected. Low-scoring comments are not
hidden — they are collapsed and grayed out but still fully readable. The system
demotes content that is useless to buyers, not content that is bad for the seller.

---

## Project structure
CommentIntelligence.Core/
Models/                         Comment, CommentClassification, ContentLabel, ClassificationResult, TrainingExample
Text/                           ITextPreprocessor, ILanguageDetector, EmbeddedStopWordProvider
Text/StopWords/                 en.txt, pl.txt (add {code}.txt per language)
Classification/                 NaiveBayesTrainer, NaiveBayesPredictor, IModelRegistry, ModelRegistry
Classification/Persistence/     NaiveBayesModelCache — JSON cache with SHA256 fingerprint invalidation
Training/                       ITrainingDataProvider: File / Stream / Composite, IModelTrainingService
Scoring/                        IVisibilityScorer, VisibilityScoringOptions
Pipeline/                       ICommentClassificationPipeline, UnsupportedLanguageBehaviour
Storage/                        IClassifiedCommentStore (interface only) + InMemory default
DependencyInjection/            AddCommentIntelligence(), MapCommentIntelligenceEndpoints()

CommentIntelligence.Demo/
Components/Pages/Home.razor     Paste a comment, see stars/label/score/language, sortable list
TrainingData/*.csv              Starter EN + PL training sets (sentiment and content-label)
ModelCache/*.json               Auto-generated on first run — add to .gitignore in production

---

## Wiring it up

```csharp
// Program.cs — this is all a host app needs

var trainingDataRoot = Path.Combine(builder.Environment.ContentRootPath, "TrainingData");
var modelCacheDirectory = Path.Combine(builder.Environment.ContentRootPath, "ModelCache");

// Register CommentIntelligence — language detection, classification pipeline,
// JSON model cache, and startup training are all handled by the package.
// Add one AddLanguage() call per supported language; each needs a sentiment CSV
// (text,stars) and a content-label CSV (text,label) as training data.
// ModelCacheDirectory persists trained models to disk so unchanged languages
// load in milliseconds on restart instead of retraining from scratch.
builder.Services.AddCommentIntelligence(options =>
{
    options.ModelCacheDirectory = modelCacheDirectory;
    options.UnsupportedLanguageBehaviour = UnsupportedLanguageBehaviour.Reject;
    options.AddLanguage("en",
        Path.Combine(trainingDataRoot, "sentiment-training-en.csv"),
        Path.Combine(trainingDataRoot, "content-label-training-en.csv"));
    options.AddLanguage("pl",
        Path.Combine(trainingDataRoot, "sentiment-training-pl.csv"),
        Path.Combine(trainingDataRoot, "content-label-training-pl.csv"));
});

// ...

// Exposes POST /admin/comment-intelligence/retrain?culture=en
// Omit ?culture to retrain all languages at once.
// Secure this route with your auth middleware before going to production.
app.MapCommentIntelligenceEndpoints();
```

Then inject wherever needed:

```csharp
@inject ICommentClassificationPipeline Pipeline
@inject IClassifiedCommentStore Store
```

---

## Classifying a comment

```csharp
// Language is auto-detected from the text using LanguageDetection.Ai,
// running against all known languages for accurate identification.
// Pass a CultureInfo explicitly to override (e.g. from the storefront's active language).
var result = Pipeline.Classify(text);

if (!result.IsSupported)
{
    // result.UnsupportedLanguageCode — e.g. "fr", "de"
    // Show the user an error, block submission, log it — your call.
    return;
}

// result.PredictedStars          — int 1..5, system-derived from text
// result.SentimentConfidence     — double 0..1
// result.ContentLabel            — ContentLabel enum (Informative, Helpful, Emotional,
//                                  Tendentious, Hateful, LowQuality)
// result.ContentLabelConfidence  — double 0..1
// result.VisibilityScore         — double 0..1, use this to sort/rank
// result.DetectedCulture         — the culture the comment was classified against
```

---

## Unsupported languages

When a comment arrives in a language with no trained model, the pipeline detects the
language first (against all known languages for accuracy), then checks
`IModelRegistry.SupportedCultures` before classifying. The behaviour is configurable:

```csharp
options.UnsupportedLanguageBehaviour = UnsupportedLanguageBehaviour.Reject; // default
```

**`Reject` (default):** returns a `CommentClassification` with `IsSupported = false`
and all scores zeroed. The host app decides what to do — block submission, show a
warning, log it.

**`Translate` (v2, not yet implemented):** will translate the text to `DefaultCulture`
before classifying, so comments in any language can be processed without per-language
training data.

> **Important:** the detector runs against all known languages, not just supported ones.
> This ensures French text is correctly identified as French rather than being
> misidentified as the closest supported language. The `IsSupported` check in the
> pipeline is the gate — the detector's only job is accurate identification.

The currently active supported languages are always available via the pipeline:

```csharp
IReadOnlyCollection<CultureInfo> supported = Pipeline.SupportedCultures;
```

This reflects the live model registry — if you add a language and retrain, the
collection updates immediately without a restart. Use it to show users which languages
are accepted (e.g. as badges in your UI).

---

## Adding a language

1. Add a stop-word file at `CommentIntelligence.Core/Text/StopWords/{code}.txt`
   (e.g. `fr.txt`, `es.txt`). The `.csproj` glob embeds all `*.txt` files in that
   folder automatically. Falls back to `en.txt` if no file exists for a culture.
2. Add two training CSVs for that language (sentiment and content-label).
3. Register the language in `AddCommentIntelligence`:
```csharp
   options.AddLanguage("fr", "sentiment-fr.csv", "content-label-fr.csv");
```

Do not mix languages in a single training file. Naive Bayes word probabilities are
per-class; mixing languages corrupts the frequency counts for both.

---

## Training data format

Two-column CSV with an optional header row. Quoted fields with embedded commas are supported.

**Sentiment** (`text,stars`) — label is an integer 1..5:
```csv
text,stars
"Broke after two days, terrible.",1
"Works exactly as described, fast delivery.",5
```

**Content label** (`text,label`) — label is a `ContentLabel` name:
```csv
text,label
"Battery lasts 8 hours, charges in 90 min via USB-C.",Informative
"Size down one — the medium runs large.",Helpful
"I am SO happy with this!!!",Emotional
"This whole brand is a scam.",Tendentious
"Only an idiot would buy this.",Hateful
"ok",LowQuality
```

Available providers:

| Provider | Use case |
|---|---|
| `FileTrainingDataProvider(path)` | Plain CSV on disk — most common |
| `StreamTrainingDataProvider(factory)` | Blob storage, embedded resource, any stream |
| `CompositeTrainingDataProvider(a, b, ...)` | Merge a base dataset with site-specific additions |

---

## Model caching

On first run, trained models are serialized to JSON in `ModelCacheDirectory`. On
subsequent startups, a SHA256 fingerprint of the current training data is compared
against the cached fingerprint. If they match, the cached model loads from disk
(milliseconds). If they differ, the model retrains and the cache is overwritten.

Set `options.ModelCacheDirectory = null` to always retrain from scratch.

---

## On-demand retrain (without restarting)

Update your training CSVs (or, in the future, point to a DB-backed
`ITrainingDataProvider`), then call:

```bash
# Retrain all languages
curl -X POST http://localhost:5258/admin/comment-intelligence/retrain

# Retrain one language
curl -X POST "http://localhost:5258/admin/comment-intelligence/retrain?culture=pl"
```

The new model is hot-swapped into the registry atomically — in-flight classification
calls finish against the old model, the next call uses the new one.

---

## Replacing the language detector

By default, `LanguageDetectionAiDetector` (backed by `LanguageDetection.Ai`) is used.
It detects against all known languages for accuracy — the pipeline's `IsSupported`
check is what gates unsupported languages, not the detector.

To override — e.g. to always use the storefront's active culture — register your own
`ILanguageDetector` **before** calling `AddCommentIntelligence`:

```csharp
builder.Services.AddSingleton<ILanguageDetector, MyCustomDetector>();
builder.Services.AddCommentIntelligence(options => { ... });
```

The package uses `TryAddSingleton` internally so it won't overwrite yours.

---

## Replacing the comment store

`InMemoryClassifiedCommentStore` is the default — fine for the demo, not for
production. Implement `IClassifiedCommentStore` against EF Core, Dapper, or whatever
persistence layer the host app uses, then register it before `AddCommentIntelligence`:

```csharp
builder.Services.AddScoped<IClassifiedCommentStore, EfClassifiedCommentStore>();
builder.Services.AddCommentIntelligence(options => { ... });
```

---

## Tuning the visibility score

```csharp
options.VisibilityScoringOptions = new VisibilityScoringOptions
{
    ContentLabelWeight = 0.5,
    ContentLabelConfidenceWeight = 0.2,
    SentimentConfidenceWeight = 0.2,
    RecencyWeight = 0.1,
    RecencyHalfLifeDays = 30,  // score halves every 30 days
    ContentLabelScores = new()
    {
        [ContentLabel.Informative] = 1.0,
        [ContentLabel.Helpful]     = 0.9,
        [ContentLabel.Emotional]   = 0.4,
        [ContentLabel.Tendentious] = 0.2,
        [ContentLabel.Hateful]     = 0.0,
        [ContentLabel.LowQuality]  = 0.1,
        [ContentLabel.Unknown]     = 0.3,
    }
};
```

---

## Known limitations / future work

- **`InMemoryClassifiedCommentStore` is not persistent.** Replace for production (see above).
- **Training data is demonstration-sized.** Naive Bayes accuracy scales with volume and
  diversity. Budget time to grow the CSVs — or eventually wire `ITrainingDataProvider`
  to a DB table that accumulates real comments — before relying on it for production ranking.
- **No peer/community verification.** Set aside for v1. The `VisibilityScore` formula
  is designed to accept an additional verification signal as a weighted input later
  without breaking the existing shape.
- **Admin endpoint has no auth.** `MapCommentIntelligenceEndpoints()` registers the
  retrain route with no authorization policy — add your own middleware or
  `.RequireAuthorization()` before going to production.
- **Translate behaviour not yet implemented.** `UnsupportedLanguageBehaviour.Translate`
  throws `NotImplementedException` — planned for v2 via a pluggable `ITranslationService`.

---

## Running the demo

```bash
dotnet run --project CommentIntelligence.Demo
```

Navigate to the root URL. Supported languages are shown as badges — comments in any
other language are rejected with a clear message. Paste a review, watch the predicted
stars, content label, detected language, and visibility score. Switch the sort dropdown
to see how the ranking changes. Low-score comments render collapsed and grayed out but
remain fully readable on click.