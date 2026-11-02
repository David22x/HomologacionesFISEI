namespace StudentsService.Models;

public class HistorialAcademico
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudianteId { get; set; }

    // Internos: identificador de la carrera en AcademicDB (referencia sin clave foranea entre bases).
    public Guid? CarreraOrigenId { get; set; }
    // Externos: carrera e institucion de origen tal como vienen en la plantilla XLSX.
    public string? CarreraOrigenNombre { get; set; }
    public string? InstitucionOrigen { get; set; }
    public int? NivelActual { get; set; }
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public Estudiante? Estudiante { get; set; }
    public ICollection<AsignaturaCursada> Asignaturas { get; set; } = new List<AsignaturaCursada>();
}
