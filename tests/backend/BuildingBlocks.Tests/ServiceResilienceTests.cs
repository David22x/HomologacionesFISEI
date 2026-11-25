using System.Data.Common;
using System.Net;
using System.Text.Json;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Resilience;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BuildingBlocks.Tests;

public sealed class ServiceResilienceTests
{
    [Theory]
    [InlineData("http", 503)]
    [InlineData("timeout", 504)]
    [InlineData("database", 503)]
    [InlineData("bug", 500)]
    public async Task Errores_de_dependencias_dejan_una_respuesta_controlada_sin_exponer_detalles(string kind, int status)
    {
        Exception error = kind switch { "http" => new HttpRequestException("SECRETO"), "timeout" => new TaskCanceledException("SECRETO"),
            "database" => new Exception("envoltorio", new UnavailableDatabase()), _ => new InvalidOperationException("SECRETO") };
        var context = new DefaultHttpContext(); context.Response.Body = new MemoryStream(); context.Request.Method = "POST";
        var middleware = new GlobalExceptionHandlerMiddleware(_ => Task.FromException(error), NullLogger<GlobalExceptionHandlerMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("SECRETO", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(status, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        if (status is 503 or 504) { Assert.Equal("15", context.Response.Headers.RetryAfter); Assert.Contains("consulta su estado", body); }
    }

    [Fact]
    public async Task Cancelacion_del_cliente_no_se_convierte_en_error_500()
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(true) };
        context.Response.Body = new MemoryStream();
        await new GlobalExceptionHandlerMiddleware(_ => throw new OperationCanceledException(), NullLogger<GlobalExceptionHandlerMiddleware>.Instance).InvokeAsync(context);
        Assert.Equal(499, context.Response.StatusCode); Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task Circuito_aisla_el_servicio_caido_y_se_recupera_sin_reiniciar()
    {
        var clock = new Clock(); var circuits = new DependencyCircuits(clock);
        using var source = new Source();
        using var client = new HttpClient(new DependencyCircuitHandler(circuits, "test") { InnerHandler = source });
        for (var i = 0; i < 3; i++) await client.GetAsync("http://caido/test");
        var blocked = await client.GetAsync("http://caido/test"); Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode); Assert.Equal(3, source.Calls);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("http://otro/test")).StatusCode);
        source.Recovered = true; clock.Advance();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("http://caido/test")).StatusCode);
        Assert.Equal(5, source.Calls);
    }

    [Fact]
    public async Task Un_guardado_fallido_no_se_repite_automaticamente()
    {
        using var source = new Source { Throw = true };
        using var client = new HttpClient(new DependencyCircuitHandler(new DependencyCircuits(TimeProvider.System), "write") { InnerHandler = source });
        await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsync("http://caido/save", new StringContent("datos")));
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task Readiness_detecta_la_caida_y_la_recuperacion_de_la_dependencia()
    {
        var up = true; var services = new ServiceCollection(); services.AddLogging();
        services.AddReadinessCheck("database", (_, _) => Task.FromResult(up));
        await using var provider = services.BuildServiceProvider(); var health = provider.GetRequiredService<HealthCheckService>();
        Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync()).Status);
        up = false; Assert.Equal(HealthStatus.Unhealthy, (await health.CheckHealthAsync()).Status);
        up = true; Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync()).Status);
    }

    [Fact]
    public void Se_limita_la_espera_normal_y_se_conserva_el_presupuesto_de_IA()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddHttpClient("normal");
        services.AddHttpClient("ia", c => c.Timeout = TimeSpan.FromSeconds(190)); services.AddServiceResilience();
        using var provider = services.BuildServiceProvider(); var factory = provider.GetRequiredService<IHttpClientFactory>();
        Assert.Equal(TimeSpan.FromSeconds(30), factory.CreateClient("normal").Timeout);
        Assert.Equal(TimeSpan.FromSeconds(190), factory.CreateClient("ia").Timeout);
    }
    private sealed class UnavailableDatabase : DbException { public override bool IsTransient => true; }
    private sealed class Clock : TimeProvider { private DateTimeOffset now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => now; public void Advance() => now = now.AddSeconds(16); }
    private sealed class Source : HttpMessageHandler {
        public int Calls; public bool Recovered; public bool Throw;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Calls++; if (Throw) throw new HttpRequestException("caído");
            return Task.FromResult(new HttpResponseMessage(request.RequestUri!.Host == "caido" && !Recovered ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        }
    }
}
