namespace HomologationService.Models;

public sealed class EjecucionReglas
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpedienteId { get; set; }
    public Guid ProfesorId { get; set; }
    public DateTime EjecutadoEn { get; set; } = DateTime.UtcNow;
    public string ResultadosJson { get; set; } = string.Empty;
}
