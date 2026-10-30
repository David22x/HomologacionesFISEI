using System.ComponentModel.DataAnnotations;
using StudentsService.Models;

namespace StudentsService.Contracts;

public sealed record EstudianteWriteRequest(
    [Required, StringLength(20)] string Cedula,
    TipoIdentificacion TipoIdentificacion,
    [Required, StringLength(150)] string Nombres,
    [Required, StringLength(150)] string Apellidos,
    TipoEstudiante Tipo,
    HistorialWriteRequest? Historial);

public sealed record EstudianteResponse(Guid Id, string Cedula, TipoIdentificacion TipoIdentificacion,
    string Nombres, string Apellidos, TipoEstudiante Tipo, string? CreadoPor, DateTime CreadoEn,
    string? ModificadoPor, DateTime ModificadoEn, HistorialResponse? Historial);

public sealed record HistorialWriteRequest(Guid? CarreraOrigenId,
    [StringLength(200)] string? CarreraOrigenNombre,
    [StringLength(200)] string? InstitucionOrigen,
    int? NivelActual,
    IReadOnlyList<AsignaturaCursadaWriteRequest>? Asignaturas);

public sealed record AsignaturaCursadaWriteRequest(Guid? AsignaturaId,
    [StringLength(30)] string? Codigo,
    [Required, StringLength(200)] string Nombre,
    decimal? Creditos,
    int? Horas,
    decimal Nota,
    [Required, StringLength(30)] string Periodo);

public sealed record HistorialResponse(Guid Id, Guid EstudianteId, Guid? CarreraOrigenId,
    string? CarreraOrigenNombre, string? InstitucionOrigen, int? NivelActual, DateTime ActualizadoEn,
    IReadOnlyList<AsignaturaCursadaResponse> Asignaturas);

public sealed record AsignaturaCursadaResponse(Guid Id, Guid? AsignaturaId, string? Codigo,
    string Nombre, decimal? Creditos, int? Horas, decimal Nota, string Periodo);
