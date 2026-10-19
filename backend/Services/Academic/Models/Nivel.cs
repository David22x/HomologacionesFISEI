namespace AcademicService.Models;

public class Nivel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MallaVersionId { get; set; }
    public int Numero { get; set; }

    public MallaVersion? MallaVersion { get; set; }
    public ICollection<Asignatura> Asignaturas { get; set; } = new List<Asignatura>();
}
