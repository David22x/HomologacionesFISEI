using HomologationService.Controllers;
using HomologationService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace HomologationService.Tests;

public sealed class ExplicacionPlantillaTests
{
    [Fact]
    public async Task OllamaSinEscuchar_endpointDevuelvePlantillaYDatosIncompletos()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var puerto = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/"), Timeout = TimeSpan.FromSeconds(2) };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var comparador = new OllamaComparadorContenidos(http, new ConfigurationBuilder().Build(), cache);
        var agente = new AgenteIaService(new MotorReglasHomologacion(new ParametrosReglas()), comparador);

        var respuesta = Assert.IsType<OkObjectResult>(await new EvaluacionesController(agente).Evaluar(Solicitud(), default));
        var resultado = Assert.IsType<ResultadoAgenteIa>(respuesta.Value);
        Assert.Equal(ResultadoHomologacion.DatosIncompletos, resultado.Reglas.Resultado);
        Assert.Null(resultado.Reglas.CoberturaContenidos);
        Assert.Empty(resultado.Cobertura.TemasAusentes);
        Assert.Contains("requiere revisión", resultado.Explicacion);
        Assert.Contains("servicio", resultado.Explicacion);
        Assert.Equal("plantilla", resultado.FuenteExplicacion);
    }

    [Fact]
    public async Task RespuestaInvalida_devuelveExplicacionSinInventarCobertura()
    {
        var agente = new AgenteIaService(new MotorReglasHomologacion(new ParametrosReglas()), new ComparadorInvalido());
        var resultado = await agente.EvaluarAsync(Solicitud());
        Assert.Equal(ResultadoHomologacion.DatosIncompletos, resultado.Reglas.Resultado);
        Assert.Null(resultado.Cobertura.Porcentaje);
        Assert.Contains("válidas", resultado.Explicacion);
    }

    [Fact]
    public async Task CancelacionDelUsuario_sePropaga()
    {
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        var agente = new AgenteIaService(new MotorReglasHomologacion(new ParametrosReglas()), new ComparadorCancelado());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agente.EvaluarAsync(Solicitud(), cancelacion.Token));
    }

    [Theory]
    [InlineData(ResultadoHomologacion.Homologable, "cumple")]
    [InlineData(ResultadoHomologacion.RechazoPorCreditos, "créditos insuficientes")]
    [InlineData(ResultadoHomologacion.RechazoPorContenidos, "contenidos insuficiente")]
    [InlineData(ResultadoHomologacion.TransversalDirecta, "transversal directa")]
    [InlineData(ResultadoHomologacion.OrigenNoAprobado, "aprobación")]
    public void PlantillaExplicaElResultado(ResultadoHomologacion decision, string texto)
    {
        var reglas = new ResultadoReglas(decision, 0.8m, null, 1m, [], "1.0.0");
        var explicacion = ExplicacionPlantilla.Crear(reglas);
        Assert.Contains(texto, explicacion);
        Assert.Contains("80%", explicacion);
        Assert.Contains("Cobertura: sin datos", explicacion);
    }

    private static SolicitudEvaluacionIa Solicitud() =>
        new(4, 4, 64, 64, true, 8, false, false,
            [new("O1", "Variables")], [new("D1", "Variables", 4)]);

    private sealed class ComparadorInvalido : IComparadorContenidosIa
    {
        public Task<IReadOnlyList<CoincidenciaIa>> CompararAsync(
            IReadOnlyList<ContenidoOrigenIa> origen, IReadOnlyList<ContenidoDestinoIa> destino, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CoincidenciaIa>>([new("inexistente", 2m, "inválido")]);
    }

    private sealed class ComparadorCancelado : IComparadorContenidosIa
    {
        public Task<IReadOnlyList<CoincidenciaIa>> CompararAsync(
            IReadOnlyList<ContenidoOrigenIa> origen, IReadOnlyList<ContenidoDestinoIa> destino, CancellationToken cancellationToken = default) =>
            Task.FromCanceled<IReadOnlyList<CoincidenciaIa>>(cancellationToken);
    }
}
