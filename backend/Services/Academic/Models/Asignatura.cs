namespace AcademicService.Models;

public class Asignatura
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MallaVersionId { get; set; }
    public Guid NivelId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Creditos { get; set; }
    public int Horas { get; set; }
    public string? UnidadOrganizacional { get; set; }

    public MallaVersion? MallaVersion { get; set; }
    public Nivel? Nivel { get; set; }
}
