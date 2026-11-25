using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Resilience;

public static class Readiness
{
    public static IServiceCollection AddReadinessCheck(this IServiceCollection services, string name,
        Func<IServiceProvider, CancellationToken, Task<bool>> probe)
    {
        services.AddHealthChecks().Add(new HealthCheckRegistration(name, sp => new Probe(sp, probe), HealthStatus.Unhealthy, ["ready"], TimeSpan.FromSeconds(3)));
        return services;
    }
    public static void MapServiceHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new {
                estado = report.Status.ToString(), comprobaciones = report.Entries.Select(x => new { nombre = x.Key, estado = x.Value.Status.ToString() })
            })
        });
    }
    private sealed class Probe(IServiceProvider services, Func<IServiceProvider, CancellationToken, Task<bool>> probe) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try { using var scope = services.CreateScope(); return await probe(scope.ServiceProvider, ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Dependencia no disponible."); }
            catch (Exception) { return HealthCheckResult.Unhealthy("Dependencia no disponible."); }
        }
    }
}
