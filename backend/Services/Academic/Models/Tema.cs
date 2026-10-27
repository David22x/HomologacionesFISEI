namespace AcademicService.Models;

public class Tema
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public int? HorasEstimadas { get; set; }

    public Asignatura? Asignatura { get; set; }
    public ICollection<Subtema> Subtemas { get; set; } = new List<Subtema>();
}