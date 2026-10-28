namespace HomologationService.Models;

// Expediente de homologacion de un estudiante hacia una carrera y malla de destino.
// EstudianteId apunta a StudentsDB y CarreraDestinoId/MallaDestinoId a AcademicDB:
// son referencias logicas validadas por HTTP, no claves foraneas.
public class Expediente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstudianteId { get; set; }
    public Guid CarreraDestinoId { get; set; }
    public Guid MallaDestinoId { get; set; }
    public EstadoExpediente Estado { get; set; } = EstadoExpediente.Borrador;

    // Profesor responsable (viene del usuario autenticado).
    public Guid ProfesorId { get; set; }
    public string ProfesorNombre { get; set; } = string.Empty;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoEn { get; set; }

    public List<Equivalencia> Equivalencias { get; set; } = new();
}
