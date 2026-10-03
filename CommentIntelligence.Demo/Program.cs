using CommentIntelligence.Core.Hosting.DependencyInjection;
using CommentIntelligence.Demo.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register CommentIntelligence — language detection, classification pipeline,
// JSON model cache, and startup training are all handled by the package.
// Add one AddLanguage() call per supported language; each needs a sentiment CSV
// (text,stars) and a content-label CSV (text,label) as training data.
// ModelCacheDirectory persists trained models to disk so unchanged languages
// load in milliseconds on restart instead of retraining from scratch.
var trainingDataRoot = Path.Combine(builder.Environment.ContentRootPath, "TrainingData");
var modelCacheDirectory = Path.Combine(builder.Environment.ContentRootPath, "ModelCache");

builder.Services.AddCommentIntelligence(options =>
{
    options.ModelCacheDirectory = modelCacheDirectory;
    options.LanguageSafetyOptions.AddLanguage(
        "en",
        [
            "fuck", "fucking", "fucked", "fucker", "fuckers",
            "shit", "shitty", "bullshit",
            "bastard", "bastards", "asshole", "assholes",
            "idiot", "idiots", "idiotic", "moron", "morons",
            "stupid", "stupidity", "dumb", "dumbass",
            "scumbag", "jerk", "loser", "liar", "liars"
        ],
        [
            "don't buy", "do not buy", "never buy", "avoid this", "stay away",
            "worst", "awful", "horrible", "terrible", "useless",
            "piece of crap", "piece of shit", "complete crap", "total garbage",
            "absolute garbage", "waste of money", "ripped off", "rip-off",
            "total scam", "complete scam", "fraud", "fraudulent"
        ]);
    options.LanguageSafetyOptions.AddLanguage(
        "pl",
        [
            "kurwa", "kurwy", "kurwą", "kurwiarz", "kurwiarze",
            "cham", "chamy", "chamski", "chamskie",
            "idiota", "idioci", "idiotą", "idiotyczny", "idiotyczne",
            "debil", "debile", "debilem", "debilny", "debilne",
            "kretyn", "kretyni", "głupek", "głupki",
            "frajer", "frajerzy", "kłamca", "kłamcy",
            "oszust", "oszuści", "oszukańczy", "gówno", "gówniany"
        ],
        [
            "nie kupuj", "nie kupować", "nigdy nie kupuj", "unikać tego",
            "trzymaj się z daleka", "najgorszy", "najgorsza", "najgorsze",
            "okropny", "okropna", "okropne", "straszny", "straszna", "straszne",
            "tragiczny", "tragiczna", "tragiczne", "bezużyteczny",
            "beznadziejny", "beznadziejna", "beznadziejne",
            "szmelc", "bubel", "kompletny bubel", "wyrzucone pieniądze",
            "strata pieniędzy", "zmarnowane pieniądze", "zostałem oszukany",
            "zostałam oszukana", "oszustwo", "produkt to gówno"
        ]);

    options.DomainRelevanceOptions.AddLanguage("en", new[]
    {
        "armchair", "bed", "bookcase", "cabinet", "chair", "coat rack",
        "desk", "dresser", "furniture", "lamp", "shelf", "shelves",
        "sofa", "table", "wardrobe"
    });
    options.DomainRelevanceOptions.AddLanguage("pl", new[]
    {
        "fotel", "łóżko", "regał", "szafka", "krzesło", "wieszak",
        "biurko", "komoda", "mebel", "meble", "lampa", "półka",
        "półki", "sofa", "stół", "szafa"
    });
    
    options.AddLanguage("en",
        Path.Combine(trainingDataRoot, "sentiment-training-en.csv"),
        Path.Combine(trainingDataRoot, "content-label-training-en.csv"));
    
    options.AddLanguage("pl",
        Path.Combine(trainingDataRoot, "sentiment-training-pl.csv"),
        Path.Combine(trainingDataRoot, "content-label-training-pl.csv"));
    
    // Add more languages if needed OR limit to one only
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();