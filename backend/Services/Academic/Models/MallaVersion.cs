namespace AcademicService.Models;

public class MallaVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CarreraId { get; set; }
    public string Version { get; set; } = string.Empty;
    public bool Vigente { get; set; }

    public Carrera? Carrera { get; set; }
    public ICollection<Nivel> Niveles { get; set; } = new List<Nivel>();
    public ICollection<Asignatura> Asignaturas { get; set; } = new List<Asignatura>();
}
