using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HomologationService.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace HomologationService.Tests;

public sealed class OllamaEstabilidadTests
{
    [Fact]
    public async Task Comparar_reutilizaTextoSinEnviarIdentificadores()
    {
        using var handler = new EmbeddingsHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var comparador = new OllamaComparadorContenidos(http, new ConfigurationBuilder().Build(), cache);
        var primera = await comparador.CompararAsync(
            [new(Guid.NewGuid().ToString(), "Variables")], [new("destino-1", "Ciclos")]);
        var segunda = await comparador.CompararAsync(
            [new(Guid.NewGuid().ToString(), "Variables")], [new("destino-2", "Ciclos")]);
        Assert.Equal(new[] { "variables", "ciclos" }, handler.Textos);
        Assert.Equal(1, handler.Llamadas);
        Assert.Equal("destino-2", Assert.Single(segunda).CodigoDestino);
        Assert.Equal(Assert.Single(primera).Similitud, segunda[0].Similitud);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmbeddingsInvalidos_producenRespaldo(bool nulo)
    {
        using var handler = new EmbeddingsHandler { VectorInvalido = nulo ? [0f, 0f] : [1f] };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var comparador = new OllamaComparadorContenidos(http, new ConfigurationBuilder().Build(), cache);
        var agente = new AgenteIaService(new MotorReglasHomologacion(new ParametrosReglas()), comparador);
        var resultado = await agente.EvaluarAsync(new(4, 4, 64, 64, true, 8, false, false,
            [new("O", "Variables")], [new("D", "Ciclos")]));
        Assert.True(resultado.FalloComparacion);
        Assert.Null(resultado.Cobertura.Porcentaje);
        Assert.Equal(ResultadoHomologacion.DatosIncompletos, resultado.Reglas.Resultado);
    }

    [Fact]
    public async Task PresupuestoAgotado_detieneLosReintentos()
    {
        using var handler = new EmbeddingsHandler { Esperar = true };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Ollama:OperationTimeoutSeconds"] = "1" }).Build();
        var comparador = new OllamaComparadorContenidos(http, config, cache);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            comparador.CompararAsync([new("O", "Variables")], [new("D", "Ciclos")]));
        Assert.Equal(1, handler.Llamadas);
    }

    private sealed class EmbeddingsHandler : HttpMessageHandler
    {
        public int Llamadas { get; private set; }
        public string[] Textos { get; private set; } = [];
        public float[]? VectorInvalido { get; init; }
        public bool Esperar { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Llamadas++;
            if (Esperar) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            using var contenido = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Textos = contenido.RootElement.GetProperty("input").EnumerateArray().Select(x => x.GetString()!).ToArray();
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { embeddings = new[] { new[] { 1f, 0f }, VectorInvalido ?? [0f, 1f] } })
            };
        }
    }
}
