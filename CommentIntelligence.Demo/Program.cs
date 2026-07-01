using System.Globalization;
using CommentIntelligence.Core.DependencyInjection;
using CommentIntelligence.Core.Training;
using CommentIntelligence.Demo.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var trainingDataRoot = Path.Combine(builder.Environment.ContentRootPath, "TrainingData");
var modelCacheDirectory = Path.Combine(builder.Environment.ContentRootPath, "ModelCache");

try
{
    builder.Services.AddCommentIntelligence(options =>
    {
        options.DefaultCulture = CultureInfo.GetCultureInfo("en");
        options.ModelCacheDirectory = modelCacheDirectory;

        options.AddLanguage(
            CultureInfo.GetCultureInfo("en"),
            new FileTrainingDataProvider(Path.Combine(trainingDataRoot, "sentiment-training-en.csv")),
            new FileTrainingDataProvider(Path.Combine(trainingDataRoot, "content-label-training-en.csv")));

        options.AddLanguage(
            CultureInfo.GetCultureInfo("pl"),
            new FileTrainingDataProvider(Path.Combine(trainingDataRoot, "sentiment-training-pl.csv")),
            new FileTrainingDataProvider(Path.Combine(trainingDataRoot, "content-label-training-pl.csv")));
    });
}
catch (Exception ex)
{
    Console.WriteLine("FAILED TO INIT COMMENT INTELLIGENCE:");
    Console.WriteLine(ex);
    throw;
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Admin-triggered retrain: re-reads the CSVs (or, in the future, a DB-backed
// ITrainingDataProvider) for one language and hot-swaps the new model into the
// registry — no app restart needed. POST /admin/retrain?culture=en (or pl, or
// omit "culture" to retrain every configured language).
app.MapPost("/admin/retrain", async (IModelTrainingService trainingService, string? culture) =>
{
    if (string.IsNullOrWhiteSpace(culture))
    {
        await trainingService.RetrainAllAsync();
        return Results.Ok(new { message = "Retrained all configured languages." });
    }

    try
    {
        await trainingService.RetrainAsync(CultureInfo.GetCultureInfo(culture));
        return Results.Ok(new { message = $"Retrained '{culture}'." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

try
{
    Console.WriteLine("Rozpoczynam trenowanie modeli klasyfikacji (Naive Bayes)...");

    using (var scope = app.Services.CreateScope())
    {
        _ = scope.ServiceProvider.GetRequiredService<ModelsTrainedMarker>();
    }

    Console.WriteLine("SUKCES: Modele zostały wytrenowane. Aplikacja gotowa do działania!");
}
catch (Exception ex)
{
    Console.WriteLine("KRYTYCZNY BŁĄD PODCZAS TRENOWANIA MODELU:");
    Console.WriteLine(ex.ToString());
}

app.Run();