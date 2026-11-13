namespace HomologationService.Services;

public enum ResultadoHomologacion
{
    Homologable,
    RechazoPorCreditos,
    RechazoPorContenidos,
    DatosIncompletos,
    TransversalDirecta,
    OrigenNoAprobado
}

public sealed record ContenidoDestino(
    string Codigo,
    decimal? Horas);

public sealed record CoincidenciaContenido(
    string CodigoDestino,
    decimal Similitud);

public sealed record EvaluacionHomologacion(
    decimal? CreditosOrigen,
    decimal? CreditosDestino,
    decimal? HorasOrigen,
    decimal? HorasDestino,
    bool OrigenAprobado,
    decimal? NotaOrigen,
    bool OrigenTransversal,
    bool DestinoTransversal,
    IReadOnlyList<ContenidoDestino> ContenidosDestino,
    IReadOnlyList<CoincidenciaContenido> Coincidencias);

public sealed record ResultadoReglas(
    ResultadoHomologacion Resultado,
    decimal? RazonCreditos,
    decimal? CoberturaContenidos,
    decimal? RazonHoras,
    IReadOnlyList<string> Advertencias,
    string VersionReglas);

public sealed class ParametrosReglas
{
    public string Version { get; init; } = "1.0.0";
    public decimal RazonCreditosMinima { get; init; } = 0.80m;
    public decimal CoberturaMinima { get; init; } = 0.80m;
    public decimal SimilitudMinima { get; init; } = 0.75m;
    public decimal? NotaMinima { get; init; }
    public decimal RazonHorasMinimaPropuesta { get; init; } = 0.80m;
}

public interface IMotorReglasHomologacion
{
    ResultadoReglas Evaluar(EvaluacionHomologacion entrada);
}

public sealed class MotorReglasHomologacion(
    ParametrosReglas parametros) : IMotorReglasHomologacion
{
    public ResultadoReglas Evaluar(EvaluacionHomologacion entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var advertencias = new List<string>();

        decimal? razonCreditos = null;
        decimal? razonHoras = null;
        decimal? cobertura = null;

        if (!entrada.OrigenAprobado
            || (parametros.NotaMinima is decimal notaMinima
                && (entrada.NotaOrigen is null || entrada.NotaOrigen < notaMinima)))
        {
            return Crear(ResultadoHomologacion.OrigenNoAprobado);
        }

        razonCreditos = CalcularRazon(
            entrada.CreditosOrigen, entrada.CreditosDestino);

        // R7: transversal directa; los créditos se muestran como referencia.
        if (entrada.OrigenTransversal && entrada.DestinoTransversal)
        {
            return Crear(ResultadoHomologacion.TransversalDirecta);
        }

        // R1 tiene prioridad sobre información incompleta de contenidos u horas.
        if (razonCreditos is decimal creditos
            && creditos < parametros.RazonCreditosMinima)
        {
            return Crear(ResultadoHomologacion.RechazoPorCreditos);
        }

        if (razonCreditos is null)
        {
            advertencias.Add("No se pudo calcular la razón de créditos.");
            return Crear(ResultadoHomologacion.DatosIncompletos);
        }

        razonHoras = CalcularRazon(
            entrada.HorasOrigen, entrada.HorasDestino);

        if (razonHoras is decimal horas
            && horas < parametros.RazonHorasMinimaPropuesta)
        {
            advertencias.Add(
                $"La razón de horas ({horas:P2}) está por debajo del mínimo propuesto.");
        }

        if (razonHoras is null)
        {
            advertencias.Add("Faltan horas para calcular la razón de carga horaria.");
        }

        if (entrada.ContenidosDestino.Count == 0)
        {
            advertencias.Add("No hay contenidos de destino para evaluar.");
            return Crear(ResultadoHomologacion.DatosIncompletos);
        }

        cobertura = CalcularCobertura(entrada);

        if (cobertura is null)
        {
            advertencias.Add("No se pudo calcular la cobertura de contenidos.");
            return Crear(ResultadoHomologacion.DatosIncompletos);
        }

        // El catálogo propone que la falta de horas totales sea incompleta.
        if (razonHoras is null)
        {
            return Crear(ResultadoHomologacion.DatosIncompletos);
        }

        var resultado = cobertura < parametros.CoberturaMinima
            ? ResultadoHomologacion.RechazoPorContenidos
            : ResultadoHomologacion.Homologable;

        return Crear(resultado);

        ResultadoReglas Crear(ResultadoHomologacion resultado) =>
            new(resultado, razonCreditos, cobertura, razonHoras,
                advertencias.ToArray(), parametros.Version);
    }

    private decimal? CalcularCobertura(EvaluacionHomologacion entrada)
    {
        var destinos = entrada.ContenidosDestino
            .GroupBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToArray();

        var coincidencias = entrada.Coincidencias
            .Where(c => c.Similitud >= parametros.SimilitudMinima)
            .Select(c => c.CodigoDestino)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Si todos los temas tienen horas positivas, se pondera por horas.
        var usarHoras = destinos.All(c => c.Horas is > 0);

        var pesoTotal = usarHoras
            ? destinos.Sum(c => c.Horas!.Value)
            : destinos.Length;

        if (pesoTotal <= 0)
            return null;

        var pesoCubierto = destinos
            .Where(c => coincidencias.Contains(c.Codigo))
            .Sum(c => usarHoras ? c.Horas!.Value : 1m);

        return pesoCubierto / pesoTotal;
    }

    private static decimal? CalcularRazon(decimal? origen, decimal? destino)
    {
        if (origen is null || destino is null || origen < 0 || destino <= 0)
            return null;

        return origen.Value / destino.Value;
    }
}