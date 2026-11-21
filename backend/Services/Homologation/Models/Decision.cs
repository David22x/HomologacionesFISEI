namespace HomologationService.Models;

public sealed class Decision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpedienteId { get; set; }
    public Guid AsignaturaDestinoId { get; set; }
    public string OrigenIdsJson { get; set; } = "[]";
    public int ResultadoCalculado { get; set; }
    public int ResultadoFinal { get; set; }
    public string? Justificacion { get; set; }
    public Guid ProfesorId { get; set; }
    public DateTime RegistradoEn { get; set; } = DateTime.UtcNow;
}
