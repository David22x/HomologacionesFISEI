using System.ComponentModel.DataAnnotations;

namespace StudentsService.Contracts;

public sealed record ExpedienteCreateRequest(Guid EstudianteId, Guid CarreraId, Guid MallaId);

public sealed record ExpedienteEstadoRequest(
    [Required, StringLength(20)] string Estado);

public sealed record ExpedienteResponse(
    Guid Id,
    Guid EstudianteId,
    string CedulaEstudiante,
    string NombresEstudiante,
    string ApellidosEstudiante,
    Guid CarreraId,
    Guid MallaId,
    string Estado,
    string? CreadoPor,
    DateTime CreadoEn,
    string? ModificadoPor,
    DateTime ModificadoEn);
