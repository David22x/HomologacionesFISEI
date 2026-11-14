namespace HomologationService.Models;

public sealed class ParHomologacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpedienteId { get; set; }
    public Guid AsignaturaCursadaId { get; set; }
    public Guid AsignaturaDestinoId { get; set; }
    public bool Precargado { get; set; }
}
