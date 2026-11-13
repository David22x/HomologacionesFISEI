using HomologationService.Services;
using Xunit;

namespace HomologationService.Tests;

public sealed class MotorReglasHomologacionTests
{
    private static MotorReglasHomologacion CrearMotor() =>
        new(new ParametrosReglas());

    private static EvaluacionHomologacion Entrada(
        decimal creditosOrigen = 4m,
        decimal creditosDestino = 5m,
        IReadOnlyList<ContenidoDestino>? contenidos = null,
        IReadOnlyList<CoincidenciaContenido>? coincidencias = null,
        decimal? horasOrigen = 80m,
        decimal? horasDestino = 80m,
        bool origenTransversal = false,
        bool destinoTransversal = false) =>
        new(
            creditosOrigen,
            creditosDestino,
            horasOrigen,
            horasDestino,
            OrigenAprobado: true,
            NotaOrigen: 8m,
            origenTransversal,
            destinoTransversal,
            contenidos ?? new[]
            {
                new ContenidoDestino("T1", 10m),
                new ContenidoDestino("T2", 10m),
                new ContenidoDestino("T3", 10m),
                new ContenidoDestino("T4", 10m),
                new ContenidoDestino("T5", 10m)
            },
            coincidencias ?? new[]
            {
                new CoincidenciaContenido("T1", 0.75m),
                new CoincidenciaContenido("T2", 0.80m),
                new CoincidenciaContenido("T3", 0.90m),
                new CoincidenciaContenido("T4", 0.85m)
            });

    [Fact]
    public void Creditos_en_el_limite_inclusivo_y_cobertura_suficiente_es_homologable()
    {
        var resultado = CrearMotor().Evaluar(Entrada());

        Assert.Equal(ResultadoHomologacion.Homologable, resultado.Resultado);
        Assert.Equal(0.80m, resultado.RazonCreditos);
        Assert.Equal(0.80m, resultado.CoberturaContenidos);
    }

    [Fact]
    public void Rechazo_por_creditos_tiene_prioridad_sobre_contenidos()
    {
        var resultado = CrearMotor().Evaluar(Entrada(
            creditosOrigen: 3.99m,
            contenidos: new[] { new ContenidoDestino("T1", 1m) },
            coincidencias: Array.Empty<CoincidenciaContenido>()));

        Assert.Equal(ResultadoHomologacion.RechazoPorCreditos, resultado.Resultado);
    }

    [Fact]
    public void Similitud_menor_al_umbral_no_cuenta_como_coincidencia()
    {
        var resultado = CrearMotor().Evaluar(Entrada(
            contenidos: new[] { new ContenidoDestino("T1", null) },
            coincidencias: new[] { new CoincidenciaContenido("T1", 0.749m) }));

        Assert.Equal(ResultadoHomologacion.RechazoPorContenidos, resultado.Resultado);
        Assert.Equal(0m, resultado.CoberturaContenidos);
    }

    [Fact]
    public void Cada_contenido_destino_se_cuenta_una_sola_vez()
    {
        var resultado = CrearMotor().Evaluar(Entrada(
            contenidos: new[]
            {
                new ContenidoDestino("T1", null),
                new ContenidoDestino("T2", null)
            },
            coincidencias: new[]
            {
                new CoincidenciaContenido("T1", 0.90m),
                new CoincidenciaContenido("T1", 0.95m)
            }));

        Assert.Equal(0.5m, resultado.CoberturaContenidos);
        Assert.Equal(ResultadoHomologacion.RechazoPorContenidos, resultado.Resultado);
    }

    [Fact]
    public void Horas_bajas_generan_advertencia_y_continuan_evaluacion()
    {
        var resultado = CrearMotor().Evaluar(Entrada(
            horasOrigen: 60m,
            horasDestino: 80m));

        Assert.Equal(ResultadoHomologacion.Homologable, resultado.Resultado);
        Assert.Contains(resultado.Advertencias, a => a.Contains("horas"));
    }

    [Fact]
    public void Transversales_aprobadas_aplican_excepcion_de_contenidos()
    {
        var resultado = CrearMotor().Evaluar(Entrada(
            creditosOrigen: 2m,
            creditosDestino: 3m,
            contenidos: Array.Empty<ContenidoDestino>(),
            coincidencias: Array.Empty<CoincidenciaContenido>(),
            origenTransversal: true,
            destinoTransversal: true));

        Assert.Equal(ResultadoHomologacion.TransversalDirecta, resultado.Resultado);
        Assert.Equal(2m / 3m, resultado.RazonCreditos);
    }

    [Fact]
    public void Sin_horas_y_sin_rechazo_de_creditos_es_dato_incompleto()
    {
        var resultado = CrearMotor().Evaluar(Entrada(
            horasOrigen: null,
            horasDestino: null));

        Assert.Equal(ResultadoHomologacion.DatosIncompletos, resultado.Resultado);
    }

    [Fact]
    public void Origen_no_aprobado_no_es_homologable()
    {
        var entrada = Entrada() with { OrigenAprobado = false };

        var resultado = CrearMotor().Evaluar(entrada);

        Assert.Equal(ResultadoHomologacion.OrigenNoAprobado, resultado.Resultado);
    }
}
