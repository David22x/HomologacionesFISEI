using HomologationService.Services;
using Xunit;

namespace HomologationService.Tests;

public sealed class AgenteIaServiceTests
{
    [Fact]
    public async Task EvaluarAsync_usaCoincidenciasValidas_y_aplicaLasReglas()
    {
        var comparador = new ComparadorFalso(new[]
        {
            new CoincidenciaIa("DES-1", 0.90m, "Coincide el contenido."),
            new CoincidenciaIa("DES-2", 0.85m, "Coincide el contenido."),
            new CoincidenciaIa("DES-3", 0.80m, "Coincide el contenido."),
            new CoincidenciaIa("DES-4", 0.75m, "Coincide el contenido."),
            new CoincidenciaIa("NO-EXISTE", 0.99m, "Código que no está en destino."),
            new CoincidenciaIa("DES-5", 1.20m, "Similitud fuera de rango.")
        });

        var agente = CrearAgente(comparador);

        var resultado = await agente.EvaluarAsync(CrearSolicitud());

        Assert.Equal(ResultadoHomologacion.Homologable, resultado.Reglas.Resultado);
        Assert.Equal(0.80m, resultado.Reglas.CoberturaContenidos);
        Assert.Equal(4, resultado.CoincidenciasSugeridas.Count);
        Assert.DoesNotContain(
            resultado.CoincidenciasSugeridas,
            x => x.CodigoDestino == "NO-EXISTE");
    }

    [Fact]
    public async Task EvaluarAsync_origenNoAprobado_respetaLaRegla()
    {
        var agente = CrearAgente(
            new ComparadorFalso(Array.Empty<CoincidenciaIa>()));

        var resultado = await agente.EvaluarAsync(
            CrearSolicitud(origenAprobado: false));

        Assert.Equal(
            ResultadoHomologacion.OrigenNoAprobado,
            resultado.Reglas.Resultado);
    }

    private static AgenteIaService CrearAgente(
        IComparadorContenidosIa comparador) =>
        new(
            new MotorReglasHomologacion(new ParametrosReglas()),
            comparador);

    private static SolicitudEvaluacionIa CrearSolicitud(
        bool origenAprobado = true) =>
        new(
            CreditosOrigen: 4m,
            CreditosDestino: 5m,
            HorasOrigen: 80m,
            HorasDestino: 80m,
            OrigenAprobado: origenAprobado,
            NotaOrigen: 8m,
            OrigenTransversal: false,
            DestinoTransversal: false,
            ContenidosOrigen: new[]
            {
                new ContenidoOrigenIa("ORI-1", "Contenido de origen.")
            },
            ContenidosDestino: new[]
            {
                new ContenidoDestinoIa("DES-1", "Contenido destino 1.", 10m),
                new ContenidoDestinoIa("DES-2", "Contenido destino 2.", 10m),
                new ContenidoDestinoIa("DES-3", "Contenido destino 3.", 10m),
                new ContenidoDestinoIa("DES-4", "Contenido destino 4.", 10m),
                new ContenidoDestinoIa("DES-5", "Contenido destino 5.", 10m)
            });

    private sealed class ComparadorFalso(
        IReadOnlyList<CoincidenciaIa> coincidencias)
        : IComparadorContenidosIa
    {
        public Task<IReadOnlyList<CoincidenciaIa>> CompararAsync(
            IReadOnlyList<ContenidoOrigenIa> origen,
            IReadOnlyList<ContenidoDestinoIa> destino,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(coincidencias);
    }
}