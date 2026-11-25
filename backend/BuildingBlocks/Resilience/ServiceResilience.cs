using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace BuildingBlocks.Resilience;

public sealed class DependencyCircuits
{
    private readonly ConcurrentDictionary<string, Circuit> circuits = new();
    private readonly TimeProvider clock;
    public DependencyCircuits(TimeProvider clock) => this.clock = clock;
    public bool IsOpen(string key) => circuits.TryGetValue(key, out var circuit) && circuit.Until > clock.GetUtcNow();
    public void Succeeded(string key) => circuits.TryRemove(key, out _);
    public void Failed(string key)
    {
        var now = clock.GetUtcNow();
        circuits.AddOrUpdate(key, _ => new(1, DateTimeOffset.MinValue), (_, old) =>
        {
            var count = old.Until > DateTimeOffset.MinValue && old.Until <= now ? 1 : old.Failures + 1;
            return count >= 3 ? new(count, now.AddSeconds(15)) : new(count, DateTimeOffset.MinValue);
        });
    }
    private sealed record Circuit(int Failures, DateTimeOffset Until);
}

public sealed class DependencyCircuitHandler(DependencyCircuits circuits, string clientName) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var key = clientName + "|" + request.RequestUri?.Authority;
        if (circuits.IsOpen(key))
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request };
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(15));
            return response;
        }
        try
        {
            var response = await base.SendAsync(request, ct);
            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests) circuits.Failed(key);
            else circuits.Succeeded(key);
            return response;
        }
        catch (HttpRequestException) { circuits.Failed(key); throw; }
        // La cancelación del usuario no se considera una caída de la dependencia.
    }
}

public static class ServiceResilience
{
    public static IServiceCollection AddServiceResilience(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DependencyCircuits>();
        services.AddHttpClient();
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client => { if (client.Timeout == TimeSpan.FromSeconds(100)) client.Timeout = TimeSpan.FromSeconds(30); });
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                if (builder.Name is "ServiceMonitor" or "OllamaHealth") return;
                builder.AdditionalHandlers.Insert(0, new DependencyCircuitHandler(builder.Services.GetRequiredService<DependencyCircuits>(), builder.Name ?? "default"));
            });
        });
        return services;
    }
}
