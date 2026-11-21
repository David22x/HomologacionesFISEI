namespace HomologationService.Models;

public sealed class EventoAuditoria
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpedienteId { get; set; }
    public Guid ActorId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string CambiosJson { get; set; } = "[]";
}
