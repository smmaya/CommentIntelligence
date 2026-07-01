# CommentIntelligence

A .NET 10 library that classifies e-commerce/social comments using a multinomial
Naive Bayes pipeline, derived from the thesis's hybrid comment model — minus the
blockchain audit trail, biometric KYC, and gamification, which belong in app-layer
code, not a classification library.

Peer/community verification is intentionally **not** in this version — see "Not
included yet" below.

## Core design decision

Stars are **not** submitted by users. They're predicted from the comment text by
the sentiment classifier. Separately, a content-label classifier determines what
*kind* of comment it is (Informative, Helpful, Emotional, Tendentious, Hateful,
LowQuality). These two axes are independent on purpose:

- A 1-star review can be `Informative` and should rank high — it's useful for a
  buying decision even though it's negative.
- A 5-star review can be `LowQuality` ("nice!!") and should rank low — it adds
  nothing.

The `VisibilityScore` (0..1) combines both axes plus recency, and is what comment
lists should sort by when "most useful" is selected. It deliberately does **not**
hide bad-for-the-seller content — only content-poor or hateful content gets
demoted, and even then it stays visible, just collapsed/grayed out.

## Project structure

```
CommentIntelligence.Core/        <- the NuGet package
  Models/                        Comment, ClassificationResult, CommentClassification, ContentLabel
  Text/                          ITextPreprocessor + culture-aware tokenizer + per-language stop words
  Classification/                Generic multinomial Naive Bayes (trainer + predictor) used for both axes
  Training/                      ITrainingDataProvider: File / Stream / Composite implementations
  Scoring/                       IVisibilityScorer — the "most useful" ranking formula
  Pipeline/                      ICommentClassificationPipeline — single entry point combining everything
  Storage/                       IClassifiedCommentStore (interface only) + InMemory default
  DependencyInjection/           AddCommentIntelligence() — one-call DI wiring

CommentIntelligence.Demo/        <- Blazor Server test harness
  Pages/TestClassifier.razor     Paste a comment, see predicted stars/label/score, sortable list
  TrainingData/*.csv             Starter English training sets (text,stars and text,label)
```

## Internationalization

There is no hardcoded language. `ITextPreprocessor` takes a `CultureInfo` and
`EmbeddedStopWordProvider` loads `Text/StopWords/{two-letter-iso-language}.txt`,
falling back to English if a language has no list yet. To add a language:

1. Add `Text/StopWords/{code}.txt` (e.g. `es.txt`, `fr.txt`, `pl.txt`) as an
   embedded resource (the `.csproj` glob already covers `*.txt` in that folder).
2. Provide training data in that language (a separate CSV — Naive Bayes models
   are per-language; don't mix languages in one training set).
3. Pass the right `CultureInfo` into `Pipeline.Classify(text, culture)`.

The Naive Bayes algorithm itself is language-agnostic — only tokenization/stop-words
are language-specific, which is the opposite of the thesis's Polish-only RoBERTa
approach.

## Training data format

Two-column CSV, optional header row:

```csv
text,stars
"Broke after two days, terrible.",1
```

```csv
text,label
"Battery lasts 8 hours and charges in 90 minutes.",Informative
```

Load from a file path (`FileTrainingDataProvider`), any `Stream` — blob storage,
embedded resource, etc. — (`StreamTrainingDataProvider`), or merge several sources
with `CompositeTrainingDataProvider` (e.g. a shipped base dataset + a site's own
corrections file).

## Wiring it up

```csharp
builder.Services.AddCommentIntelligence(options =>
{
    options.SentimentTrainingDataProvider = new FileTrainingDataProvider(sentimentCsvPath);
    options.ContentLabelTrainingDataProvider = new FileTrainingDataProvider(contentLabelCsvPath);
    // optional: options.VisibilityScoringOptions.RecencyHalfLifeDays = 14;
});
```

Then inject `ICommentClassificationPipeline` to classify text and
`IClassifiedCommentStore` to persist/sort comments.

## Known v1 limitations (by design, to revisit)

- **Training runs synchronously at DI registration time.** Fine for small/medium
  CSVs; for large datasets or slow I/O providers, move training into an
  `IHostedService` so it doesn't block app startup.
- **`InMemoryClassifiedCommentStore` is not persistent.** Implement
  `IClassifiedCommentStore` against EF Core / Dapper / whatever for production —
  the interface has no EF dependency baked in.
- **No peer/community verification yet** — set aside per your decision to keep v1
  scoped to the system-only Naive Bayes pipeline. The model and score are designed
  so a verification layer could plug in later as an additional signal into
  `VisibilityScorer` without breaking the existing shape.
- **No model persistence/versioning** — models retrain from the CSVs every time the
  app starts. Add `NaiveBayesModel` JSON serialization if you want to train once and
  load a cached model.
- **Starter training datasets are intentionally small** (demonstration-sized, not
  production-sized). Naive Bayes accuracy scales with training data volume and
  diversity — budget time to grow these before relying on it for real ranking
  decisions.

## Running the demo

```
dotnet run --project CommentIntelligence.Demo
```

Navigate to the root URL, paste a review, and watch the predicted stars/label/score.
Add a few comments and switch the sort dropdown to see "Most useful" reorder them —
low-`VisibilityScore` comments render grayed out but stay fully readable, matching
the thesis's "don't hide, demote" principle.

> Note: this was scaffolded without a local .NET SDK available to compile/verify,
> so do a `dotnet build` pass after pulling it in — flag anything that needs fixing
> and I'll patch it.
