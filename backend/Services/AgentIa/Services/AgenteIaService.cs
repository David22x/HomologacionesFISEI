namespace HomologationService.Services;

public sealed record SolicitudEvaluacionIa(
    decimal? CreditosOrigen,
    decimal? CreditosDestino,
    decimal? HorasOrigen,
    decimal? HorasDestino,
    bool OrigenAprobado,
    decimal? NotaOrigen,
    bool OrigenTransversal,
    bool DestinoTransversal,
    IReadOnlyList<ContenidoOrigenIa> ContenidosOrigen,
    IReadOnlyList<ContenidoDestinoIa> ContenidosDestino);

public sealed record TemaCoberturaIa(
    string CodigoDestino,
    string Descripcion,
    decimal? Horas,
    decimal? Similitud,
    string? CodigoOrigen,
    string? Evidencia);

public sealed record CoberturaContenidosIa(
    decimal? Porcentaje,
    IReadOnlyList<TemaCoberturaIa> TemasCoincidentes,
    IReadOnlyList<TemaCoberturaIa> TemasAusentes);

public sealed record ResultadoAgenteIa(
    ResultadoReglas Reglas,
    IReadOnlyList<CoincidenciaIa> CoincidenciasSugeridas,
    CoberturaContenidosIa Cobertura);

public interface IAgenteIaService
{
    Task<ResultadoAgenteIa> EvaluarAsync(
        SolicitudEvaluacionIa solicitud,
        CancellationToken cancellationToken = default);
}

public sealed class AgenteIaService : IAgenteIaService
{
    private readonly IMotorReglasHomologacion motor;
    private readonly IComparadorContenidosIa comparador;
    private readonly ParametrosReglas parametros;

    public AgenteIaService(
        IMotorReglasHomologacion motor,
        IComparadorContenidosIa comparador,
        ParametrosReglas? parametros = null)
    {
        this.motor = motor;
        this.comparador = comparador;
        this.parametros = parametros ?? new ParametrosReglas();
    }

    public async Task<ResultadoAgenteIa> EvaluarAsync(
        SolicitudEvaluacionIa solicitud,
        CancellationToken cancellationToken = default)
    {
        var evaluacion = new EvaluacionHomologacion(
            solicitud.CreditosOrigen,
            solicitud.CreditosDestino,
            solicitud.HorasOrigen,
            solicitud.HorasDestino,
            solicitud.OrigenAprobado,
            solicitud.NotaOrigen,
            solicitud.OrigenTransversal,
            solicitud.DestinoTransversal,
            Array.Empty<ContenidoDestino>(),
            Array.Empty<CoincidenciaContenido>());

        var resultadoBase = motor.Evaluar(evaluacion);
        if (resultadoBase.Resultado != ResultadoHomologacion.DatosIncompletos
            || resultadoBase.RazonCreditos is null
            || solicitud.ContenidosDestino.Count == 0)
        {
            return new ResultadoAgenteIa(
                resultadoBase,
                Array.Empty<CoincidenciaIa>(),
                new CoberturaContenidosIa(null, Array.Empty<TemaCoberturaIa>(), Array.Empty<TemaCoberturaIa>()));
        }

        var tieneOrigen = solicitud.ContenidosOrigen.Count > 0;
        var coincidenciasIa = tieneOrigen
            ? await comparador.CompararAsync(
                solicitud.ContenidosOrigen,
                solicitud.ContenidosDestino,
                cancellationToken)
            : Array.Empty<CoincidenciaIa>();

        var codigosDestino = solicitud.ContenidosDestino
            .Select(x => x.Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var coincidenciasValidas = coincidenciasIa
            .Where(x => codigosDestino.Contains(x.CodigoDestino)
                && x.Similitud >= 0m
                && x.Similitud <= 1m)
            .GroupBy(x => x.CodigoDestino, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Similitud).First())
            .ToArray();

        var resultadoReglas = motor.Evaluar(evaluacion with
        {
            ContenidosDestino = solicitud.ContenidosDestino
                .Select(x => new ContenidoDestino(x.Codigo, x.Horas))
                .ToArray(),
            TieneContenidosOrigen = tieneOrigen,
            Coincidencias = coincidenciasValidas
                .Select(x => new CoincidenciaContenido(x.CodigoDestino, x.Similitud))
                .ToArray()
        });

        if (!tieneOrigen)
        {
            return new ResultadoAgenteIa(
                resultadoReglas,
                Array.Empty<CoincidenciaIa>(),
                new CoberturaContenidosIa(
                    resultadoReglas.CoberturaContenidos,
                    Array.Empty<TemaCoberturaIa>(),
                    Array.Empty<TemaCoberturaIa>()));
        }

        return CrearResultado(resultadoReglas, coincidenciasValidas, solicitud.ContenidosDestino);
    }

    private ResultadoAgenteIa CrearResultado(
        ResultadoReglas reglas,
        IReadOnlyList<CoincidenciaIa> coincidencias,
        IReadOnlyList<ContenidoDestinoIa> contenidosDestino)
    {
        var porCodigo = coincidencias.ToDictionary(
            x => x.CodigoDestino,
            StringComparer.OrdinalIgnoreCase);

        var temasCoincidentes = new List<TemaCoberturaIa>();
        var temasAusentes = new List<TemaCoberturaIa>();
        foreach (var tema in contenidosDestino
            .GroupBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()))
        {
            porCodigo.TryGetValue(tema.Codigo, out var coincidencia);
            var detalle = new TemaCoberturaIa(
                tema.Codigo,
                tema.Descripcion,
                tema.Horas,
                coincidencia?.Similitud,
                coincidencia?.CodigoOrigen,
                coincidencia?.Evidencia);

            if (coincidencia is not null
                && coincidencia.Similitud >= parametros.SimilitudMinima)
                temasCoincidentes.Add(detalle);
            else
                temasAusentes.Add(detalle);
        }

        return new ResultadoAgenteIa(
            reglas,
            coincidencias,
            new CoberturaContenidosIa(
                reglas.CoberturaContenidos,
                temasCoincidentes,
                temasAusentes));
    }
}