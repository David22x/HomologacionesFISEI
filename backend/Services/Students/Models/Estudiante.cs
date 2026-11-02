namespace StudentsService.Models;

public enum TipoEstudiante
{
    Interno = 0,
    Externo = 1
}

public enum TipoIdentificacion
{
    Cedula = 0,
    Pasaporte = 1
}

public class Estudiante
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Para TipoIdentificacion.Cedula: 10 digitos. Para Pasaporte: numero alfanumerico (estudiantes externos).
    public string Cedula { get; set; } = string.Empty;
    public TipoIdentificacion TipoIdentificacion { get; set; } = TipoIdentificacion.Cedula;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public TipoEstudiante Tipo { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public string? ModificadoPor { get; set; }
    public DateTime ModificadoEn { get; set; } = DateTime.UtcNow;

    public HistorialAcademico? Historial { get; set; }
}
