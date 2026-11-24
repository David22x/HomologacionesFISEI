namespace BuildingBlocks.AcademicImports;

public sealed record FilaRecord(string Nombre, string Carrera, string Periodo, decimal? Nota, string Estado, int Pagina,
    bool Seleccionado = true, Guid? AsignaturaId = null, decimal? Creditos = null, int? Horas = null, bool Reemplazar = false,
    string? Evidencia = null, decimal? NotaOriginal = null, decimal? EscalaOriginal = null, decimal? CreditosDocumento = null);
public sealed record PerfilRecord(string Nombres = "", string Apellidos = "", string NombreCompleto = "", string Carrera = "",
    string Institucion = "", int TipoIdentificacion = 0, int? NivelActual = null, decimal? EscalaMaxima = null);
public sealed record VistaRecord(string Identificacion, IReadOnlyList<FilaRecord> Filas, IReadOnlyList<string> Advertencias, PerfilRecord? Perfil = null);
public sealed record SolicitudExtraccionRecord(string Texto);
