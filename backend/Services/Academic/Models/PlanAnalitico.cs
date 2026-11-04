namespace AcademicService.Models;

/// <summary>PDF original y trazabilidad del último plan analítico cargado para una asignatura.</summary>
public sealed class PlanAnalitico
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AsignaturaId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public byte[] Contenido { get; set; } = Array.Empty<byte>();
    public DateTime SubidoEn { get; set; } = DateTime.UtcNow;
    public string SubidoPor { get; set; } = string.Empty;

    public Asignatura? Asignatura { get; set; }
}
