using System.Globalization;
using CommentIntelligence.Core.Training;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CommentIntelligence.Core.DependencyInjection;

public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the CommentIntelligence admin endpoints. Currently exposes:
    ///
    ///   POST /admin/comment-intelligence/retrain
    ///   POST /admin/comment-intelligence/retrain?culture=pl
    ///
    /// Call this in your app's pipeline setup after <c>app.UseRouting()</c> (or after
    /// <c>app.MapRazorComponents()</c> for Blazor Web App projects). Host apps can protect
    /// these endpoints with their own auth middleware — the package doesn't add auth
    /// requirements so it works with whatever auth scheme the host app uses.
    ///
    /// Future admin endpoints (e.g. model stats, supported languages list) will be added
    /// here without requiring changes to host apps that already call this method.
    /// </summary>
    public static IEndpointRouteBuilder MapCommentIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/admin/comment-intelligence/retrain", async (
            IModelTrainingService trainingService,
            string? culture) =>
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

        return endpoints;
    }
}