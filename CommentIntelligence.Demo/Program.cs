using CommentIntelligence.Core.DependencyInjection;
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

// Exposes POST /admin/comment-intelligence/retrain?culture=en
// Call without ?culture to retrain all languages at once.
// Secure this route with your auth middleware before going to production.
app.MapCommentIntelligenceEndpoints();

app.Run();