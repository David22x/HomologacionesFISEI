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

public sealed record ResultadoAgenteIa(
    ResultadoReglas Reglas,
    IReadOnlyList<CoincidenciaIa> CoincidenciasSugeridas);

public interface IAgenteIaService
{
    Task<ResultadoAgenteIa> EvaluarAsync(
        SolicitudEvaluacionIa solicitud,
        CancellationToken cancellationToken = default);
}

public sealed class AgenteIaService(
    IMotorReglasHomologacion motor,
    IComparadorContenidosIa comparador) : IAgenteIaService
{
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
            return new ResultadoAgenteIa(resultadoBase, Array.Empty<CoincidenciaIa>());

        var tieneOrigen = solicitud.ContenidosOrigen.Count > 0;
        var coincidenciasIa = tieneOrigen
            ? await comparador.CompararAsync(solicitud.ContenidosOrigen, solicitud.ContenidosDestino, cancellationToken)
            : Array.Empty<CoincidenciaIa>();

        var codigosDestino = solicitud.ContenidosDestino
            .Select(x => x.Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var coincidenciasValidas = coincidenciasIa
            .Where(x =>
                codigosDestino.Contains(x.CodigoDestino)
                && x.Similitud >= 0m
                && x.Similitud <= 1m)
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

        return new ResultadoAgenteIa(resultadoReglas, coincidenciasValidas);
    }
}
