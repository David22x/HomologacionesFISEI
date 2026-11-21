using System.Globalization;

namespace HomologationService.Services;

public static class ExplicacionPlantilla
{
    public static string Crear(ResultadoReglas reglas)
    {
        var decision = reglas.Resultado switch
        {
            ResultadoHomologacion.Homologable => "La propuesta cumple los requisitos de créditos y cobertura de contenidos.",
            ResultadoHomologacion.RechazoPorCreditos => "La propuesta se rechaza por créditos insuficientes.",
            ResultadoHomologacion.RechazoPorContenidos => "La propuesta se rechaza por cobertura de contenidos insuficiente.",
            ResultadoHomologacion.TransversalDirecta => "La propuesta aplica como transversal directa.",
            ResultadoHomologacion.OrigenNoAprobado => "La asignatura de origen no cumple el requisito de aprobación.",
            _ => "La propuesta requiere revisión porque faltan datos para completar la evaluación."
        };
        return $"{decision} Créditos: {Porcentaje(reglas.RazonCreditos)}. " +
            $"Cobertura: {Porcentaje(reglas.CoberturaContenidos)}. Horas: {Porcentaje(reglas.RazonHoras)}. " +
            $"Reglas {reglas.VersionReglas}." +
            (reglas.Advertencias.Count == 0 ? "" : " Advertencias: " + string.Join(" ", reglas.Advertencias));
    }

    private static string Porcentaje(decimal? valor) =>
        valor.HasValue ? (valor.Value * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%" : "sin datos";
}
