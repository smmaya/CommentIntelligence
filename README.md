# CommentIntelligence

A .NET 10 library that classifies e-commerce and social comments using language-aware
Naive Bayes models. It derives star ratings and content labels directly from review
text — no user-submitted stars and no peer voting required.

## Core design decision

**Stars are not submitted by users.** They are predicted from the comment text by the
sentiment classifier. Separately, a content-label classifier determines what *kind* of
comment it is. These two axes are independent on purpose:

- A 1-star review can be `Informative` and rank high — useful for a buying decision
  even though it is negative.
- A 5-star review can be `LowQuality` ("nice!!") and rank low — adds nothing.

Content labels are independently scored. A comment may receive one, two, or three
labels; the primary label is dominant and the other labels are secondary evidence.
The `VisibilityScore` (0..1) uses the primary label, confidence, stars, and recency
decay, and is what comment lists sort by when "most useful" is selected. Low-scoring
comments are collapsed in the Demo but remain fully readable.

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
Components/Pages/Home.razor     Paste a comment, see stars/labels/score/language, sortable list
TrainingData/*.csv              Starter EN + PL training sets (sentiment and content-label)
ModelCache/*.json               Auto-generated on first run — add to .gitignore in production

---

## NuGet consumer quick start

`CommentIntelligence.Core` can be consumed by an ASP.NET Core, Blazor, worker, or
other .NET host. The package trains its models from data supplied by the host; it
does not include application-specific training data or a database implementation.

### 1. Install the package

```bash
dotnet add package CommentIntelligence.Core
```

The package currently targets .NET 10. Use a compatible target framework in the
consuming project.

### 2. Add training data

Create one sentiment CSV and one content-label CSV per supported language. Mark the
files as content and copy them to the output directory, or use an
`ITrainingDataProvider` for another source such as blob storage or a database:

```xml
<ItemGroup>
  <None Update="TrainingData\sentiment-en.csv"
        CopyToOutputDirectory="PreserveNewest" />
  <None Update="TrainingData\content-label-en.csv"
        CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

See [Training data format](#training-data-format) for the required columns and
labels. Do not put secrets or customer data in a publicly distributed package.

### 3. Register the service

In `Program.cs`, configure the model cache, languages, and (recommended) domain
relevance terms:

```csharp
using CommentIntelligence.Core.Hosting.DependencyInjection;
using CommentIntelligence.Core.Pipeline;

var trainingRoot = Path.Combine(builder.Environment.ContentRootPath, "TrainingData");
var modelCache = Path.Combine(builder.Environment.ContentRootPath, "ModelCache");

builder.Services.AddCommentIntelligence(options =>
{
    options.ModelCacheDirectory = modelCache;
    options.UnsupportedLanguageBehaviour = UnsupportedLanguageBehaviour.Reject;

    options.AddLanguage(
        "en",
        Path.Combine(trainingRoot, "sentiment-en.csv"),
        Path.Combine(trainingRoot, "content-label-en.csv"));

    options.DomainRelevanceOptions.AddLanguage("en",
        ["sofa", "table", "chair", "desk", "wardrobe", "furniture"]);
});
```

Register every language for which you have both training files. Relevance terms are
language-specific and should describe the host application's domain; an empty
language vocabulary leaves that language unrestricted.

### 4. Wait for model training

Models train through a hosted service during application startup. Do not classify
requests until the models are ready. The package registers a health check named
`comment-intelligence-models`; expose it through the host application's health
endpoint:

```csharp
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");
```

The package also exposes `IModelRegistry.IsReady` for application-specific readiness
checks.

### 5. Classify and handle the result

Inject `ICommentClassificationPipeline` into a controller, endpoint, Razor component,
or application service:

```csharp
public sealed class ReviewService(ICommentClassificationPipeline pipeline)
{
    public CommentClassification? Classify(string text)
    {
        var result = pipeline.Classify(text);

        if (!result.IsSupported)
            return null; // block or ask the user to choose a supported language

        if (result.IsLowInformation)
            return null; // request product-specific detail; do not store normally

        return result;
    }
}
```

For accepted results, use `PredictedStars`, `ContentLabel`,
`ContentLabels`, `IsAbusive`, `IsPromotional`, `IsUsefulForBuyer`, and
`VisibilityScore` according to the host application's moderation and ranking rules.
Do not treat a predicted star value as user-supplied evidence.

### 6. Secure retraining in production

Optional retraining endpoints can be mapped after authentication and authorization
are configured:

```csharp
app.MapCommentIntelligenceEndpoints("CommentIntelligenceAdmin");
```

Protect the `CommentIntelligenceAdmin` policy and keep training files and model-cache
directories outside publicly served static-file directories. The default in-memory
comment store is suitable for demos only; production applications should provide
their own `IClassifiedCommentStore`.

### 7. Verify the integration

Run the host and confirm that:

1. The health check reports models ready.
2. A supported-language product review produces a classification.
3. An unsupported language returns `IsSupported = false`.
4. A generic or out-of-domain comment returns `IsLowInformation = true`.
5. The model cache is writable and is reused after restart.

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
// Content labels are trained as independent one-vs-rest models, so secondary
// labels can be returned even though the CSV stores one primary label per row.
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

For production, require an authorization policy on retraining:

```csharp
app.MapCommentIntelligenceEndpoints("CommentIntelligenceAdmin");
```

The package also registers the `comment-intelligence-models` health check. Map
the host application's health endpoint to expose whether models are ready.

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
// result.ContentLabel            — primary ContentLabel enum
// result.ContentLabelConfidence  — double 0..1
// result.ContentLabels           — primary plus up to two secondary labels
// result.ContentLabelProbabilities — independent probability per known label
// result.VisibilityScore         — double 0..1, use this to sort/rank
// result.DetectedCulture         — the culture the comment was classified against
```

`IModelRegistry.IsReady` can be used by health checks or startup diagnostics to
distinguish a configured service from one whose models have actually been trained.
The pipeline throws a clear `InvalidOperationException` if called before models are ready.

Comments with no usable tokens are not classified from class priors. They return
`PredictedStars = 0`, `ContentLabel = Unknown`, and `VisibilityScore = 0`; host
applications should request more detail rather than storing them as normally classified
comments.

The Demo trims leading and trailing whitespace, rejects blank comments, and limits
input to 5,000 characters. Internal spaces and line breaks are preserved.

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

**Content label** (`text,label`) — label is the primary `ContentLabel` name:
```csv
text,label
"Battery lasts 8 hours, charges in 90 min via USB-C.",Informative
"Size down one — the medium runs large.",Helpful
"I am SO happy with this!!!",Emotional
"This whole brand is a scam.",Tendentious
"Only an idiot would buy this.",Hateful
"ok",LowQuality
```

The current file format stores one primary label per row. Runtime multi-label output
is produced by independent one-vs-rest models and may contain one, two, or three
labels. The primary label remains authoritative for visibility scoring.

Available providers:

| Provider | Use case |
|---|---|
| `FileTrainingDataProvider(path)` | Plain CSV on disk — most common |
| `StreamTrainingDataProvider(factory)` | Blob storage, embedded resource, any stream |
| `CompositeTrainingDataProvider(a, b, ...)` | Merge a base dataset with site-specific additions |

---

## Model caching

On first run, trained models are serialized to JSON in `ModelCacheDirectory`. Content
models include one cached model per label. On subsequent startups, version,
configuration, and SHA256 fingerprints are compared with the cache metadata. Matching
caches load quickly; changed data or model configuration triggers retraining. Cache
writes are atomic and corrupt or incompatible caches are ignored.

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

## Safety, limitations, and future work

- **Safety overrides are host-configured.** Explicit abusive terms force the
  `Hateful` label, force 1 star, and receive a low visibility cap. Strong-negative
  phrases can also force 1 star. Configure vocabulary per language.

- **`InMemoryClassifiedCommentStore` is not persistent.** Replace for production (see above).

- **Training data is synthetic and demonstration-sized.** Accuracy depends on volume,
  diversity, grammar, and cross-language alignment. The Demo corpora are balanced
  fixtures, not production evidence.

- **Independent labels are inferred.** The current CSV stores one primary label per
  row, so secondary labels are learned through one-vs-rest negatives rather than
  explicit multi-label annotations.
- **No peer/community verification.** Set aside for v1. The `VisibilityScore` formula
  is designed to accept an additional verification signal as a weighted input later
  without breaking the existing shape.
- **Admin endpoint requires deliberate authorization.** Pass an authorization policy
  to `MapCommentIntelligenceEndpoints("CommentIntelligenceAdmin")` before production.
- **Translate behaviour not yet implemented.** `UnsupportedLanguageBehaviour.Translate`
  throws `NotImplementedException` — planned for v2 via a pluggable `ITranslationService`.

---

## Running the demo

```bash
dotnet run --project CommentIntelligence.Demo
```

Navigate to the root URL. Supported languages are shown as badges — comments in any
other language are rejected with a clear message. Paste a review, watch the predicted
stars, primary and secondary labels, detected language, and visibility score. Switch the sort dropdown
to see how the ranking changes. Low-score comments render collapsed and grayed out but
remain fully readable on click.