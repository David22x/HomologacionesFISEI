namespace AcademicService.Models;

public enum EstadoMalla { Borrador = 0, Publicada = 1 }

public class MallaVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CarreraId { get; set; }
    public string Version { get; set; } = string.Empty;
    public bool Vigente { get; set; }

    public EstadoMalla Estado { get; set; } = EstadoMalla.Borrador;
    public string? ModificadoPor { get; set; }
    public DateTime ModificadoEn { get; set; } = DateTime.UtcNow;

    public Carrera? Carrera { get; set; }
    public ICollection<Nivel> Niveles { get; set; } = new List<Nivel>();
    public ICollection<Asignatura> Asignaturas { get; set; } = new List<Asignatura>();
}