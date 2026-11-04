using System.ComponentModel.DataAnnotations;

namespace AcademicService.Contracts;

/// <summary>Datos para crear un tema en una asignatura.</summary>
public sealed record CreateTemaRequest(
    [Required, StringLength(300)] string Nombre,
    [Range(1, int.MaxValue)] int Orden,
    [Range(1, int.MaxValue)] int? HorasEstimadas);

/// <summary>Datos para actualizar un tema existente.</summary>
public sealed record UpdateTemaRequest(
    [Required, StringLength(300)] string Nombre,
    [Range(1, int.MaxValue)] int Orden,
    [Range(1, int.MaxValue)] int? HorasEstimadas);

/// <summary>Datos para crear un subtema en un tema.</summary>
public sealed record CreateSubtemaRequest(
    [Required, StringLength(300)] string Nombre,
    [Range(1, int.MaxValue)] int Orden,
    [Range(1, int.MaxValue)] int? HorasEstimadas);

/// <summary>Datos para actualizar un subtema existente.</summary>
public sealed record UpdateSubtemaRequest(
    [Required, StringLength(300)] string Nombre,
    [Range(1, int.MaxValue)] int Orden,
    [Range(1, int.MaxValue)] int? HorasEstimadas);
