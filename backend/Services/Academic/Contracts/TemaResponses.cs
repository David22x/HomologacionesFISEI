namespace AcademicService.Contracts;

/// <summary>Representa un tema de una asignatura.</summary>
public sealed record TemaResponse(
    Guid Id,
    Guid AsignaturaId,
    string Nombre,
    int Orden,
    int? HorasEstimadas);

/// <summary>Representa un subtema de un tema.</summary>
public sealed record SubtemaResponse(
    Guid Id,
    Guid TemaId,
    string Nombre,
    int Orden,
    int? HorasEstimadas);
