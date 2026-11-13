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
        var coincidenciasIa = await comparador.CompararAsync(
            solicitud.ContenidosOrigen,
            solicitud.ContenidosDestino,
            cancellationToken);

        var codigosDestino = solicitud.ContenidosDestino
            .Select(x => x.Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var coincidenciasValidas = coincidenciasIa
            .Where(x =>
                codigosDestino.Contains(x.CodigoDestino)
                && x.Similitud >= 0m
                && x.Similitud <= 1m)
            .ToArray();

        var evaluacion = new EvaluacionHomologacion(
            solicitud.CreditosOrigen,
            solicitud.CreditosDestino,
            solicitud.HorasOrigen,
            solicitud.HorasDestino,
            solicitud.OrigenAprobado,
            solicitud.NotaOrigen,
            solicitud.OrigenTransversal,
            solicitud.DestinoTransversal,
            solicitud.ContenidosDestino
                .Select(x => new ContenidoDestino(x.Codigo, x.Horas))
                .ToArray(),
            coincidenciasValidas
                .Select(x => new CoincidenciaContenido(
                    x.CodigoDestino, x.Similitud))
                .ToArray());

        var resultadoReglas = motor.Evaluar(evaluacion);

        return new ResultadoAgenteIa(resultadoReglas, coincidenciasValidas);
    }
}