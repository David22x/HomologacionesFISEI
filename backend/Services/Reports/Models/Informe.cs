namespace ReportsService.Models;

public sealed class Informe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpedienteId { get; set; }
    public int Version { get; set; }
    public Guid GeneradoPor { get; set; }
    public DateTime GeneradoEn { get; set; } = DateTime.UtcNow;
    public string HuellaSha256 { get; set; } = string.Empty;
    public string HuellaFuente { get; set; } = string.Empty;
    public string DatosJson { get; set; } = string.Empty;
    public byte[] Pdf { get; set; } = [];
}
