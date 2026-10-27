namespace AcademicService.Models;

public class Subtema
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public int? HorasEstimadas { get; set; }

    public Tema? Tema { get; set; }
}