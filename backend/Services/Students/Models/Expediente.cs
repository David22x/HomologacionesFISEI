namespace StudentsService.Models;

public sealed class Expediente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudianteId { get; set; }
    public Estudiante Estudiante { get; set; } = null!;
    public Guid CarreraId { get; set; }
    public Guid MallaId { get; set; }
    public string Estado { get; set; } = "Pendiente";
    public string? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public string? ModificadoPor { get; set; }
    public DateTime ModificadoEn { get; set; } = DateTime.UtcNow;
}
