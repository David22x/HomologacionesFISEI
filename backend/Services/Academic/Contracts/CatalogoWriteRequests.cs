using System.ComponentModel.DataAnnotations;

namespace AcademicService.Contracts;

public sealed record CarreraWriteRequest(
    [Required, StringLength(20)] string Codigo,
    [Required, StringLength(200)] string Nombre,
    bool EsDemo);

public sealed record MallaWriteRequest(
    [Required, StringLength(20)] string Version);

public sealed record NivelWriteRequest(
    [Range(1, int.MaxValue)] int Numero);

public sealed record AsignaturaWriteRequest(
    Guid NivelId,
    [Required, StringLength(30)] string Codigo,
    [Required, StringLength(200)] string Nombre,
    [Range( 0.01, 999.99)] decimal Creditos,
    [Range(1, int.MaxValue)] int Horas,
    [StringLength(100)] string? UnidadOrganizacional);
