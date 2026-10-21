namespace AcademicService.Contracts;

public sealed record CarreraCatalogoResponse(
    Guid Id,
    string Codigo,
    string Nombre,
    bool EsDemo);

public sealed record MallaCatalogoResponse(
    Guid Id,
    Guid CarreraId,
    string Version,
    bool Vigente);

public sealed record NivelCatalogoResponse(
    Guid Id,
    Guid MallaVersionId,
    int Numero);

public sealed record AsignaturaCatalogoResponse(
    Guid Id,
    Guid MallaVersionId,
    Guid NivelId,
    int NivelNumero,
    string Codigo,
    string Nombre,
    decimal Creditos,
    int Horas,
    string? UnidadOrganizacional);
