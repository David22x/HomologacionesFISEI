namespace StudentsService.Models;

public class AsignaturaCursada
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HistorialAcademicoId { get; set; }

    // Internos: identificador de la asignatura en AcademicDB (sin clave foranea entre bases).
    public Guid? AsignaturaId { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal? Creditos { get; set; }
    public int? Horas { get; set; }
    public decimal Nota { get; set; }
    public string Periodo { get; set; } = string.Empty;

    public HistorialAcademico? Historial { get; set; }
}
