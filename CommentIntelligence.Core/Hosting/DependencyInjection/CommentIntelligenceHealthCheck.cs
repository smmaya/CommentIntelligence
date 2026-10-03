using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommentIntelligence.Core.Hosting.DependencyInjection;

internal sealed class CommentIntelligenceHealthCheck : IHealthCheck
{
    private readonly IModelRegistry _registry;

    public CommentIntelligenceHealthCheck(IModelRegistry registry)
    {
        _registry = registry;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_registry.IsReady
            ? HealthCheckResult.Healthy("CommentIntelligence models are ready.")
            : HealthCheckResult.Unhealthy("CommentIntelligence models are not ready."));
    }
}
