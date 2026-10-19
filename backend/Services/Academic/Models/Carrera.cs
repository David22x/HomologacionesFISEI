namespace AcademicService.Models;

public class Carrera
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsDemo { get; set; }

    public ICollection<MallaVersion> Mallas { get; set; } = new List<MallaVersion>();
}
