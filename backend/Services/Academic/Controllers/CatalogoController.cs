using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcademicService.Data;
using AcademicService.Contracts;
using BuildingBlocks.Roles;

namespace AcademicService.Controllers;

[ApiController]
[Route("api/v1/catalogo")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public class CatalogoController : ControllerBase
{
    private readonly AcademicDbContext _context;

    public CatalogoController(AcademicDbContext context)
    {
        _context = context;
    }

    // GET: api/v1/catalogo/carreras
    /// <summary>Lista las carreras disponibles para consultar.</summary>
    [HttpGet("carreras")]
    [ProducesResponseType(typeof(IReadOnlyList<CarreraCatalogoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CarreraCatalogoResponse>>> GetCarreras(CancellationToken ct)
    {
        var carreras = await _context.Carreras
            .OrderBy(c => c.EsDemo)
            .ThenBy(c => c.Nombre)
            .Select(c => new CarreraCatalogoResponse(
                c.Id,
                c.Codigo,
                c.Nombre,
                c.EsDemo))
            .ToListAsync(ct);

        return Ok(carreras);
    }

    // GET: api/v1/catalogo/carreras/{carreraId}/mallas
    /// <summary>Lista las versiones de malla de una carrera.</summary>
    /// <param name="carreraId">Identificador de la carrera.</param>
    /// <param name="ct">Token para cancelar la solicitud.</param>
    [HttpGet("carreras/{carreraId:guid}/mallas")]
    [ProducesResponseType(typeof(IReadOnlyList<MallaCatalogoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MallaCatalogoResponse>>> GetMallasPorCarrera(
        Guid carreraId,
        CancellationToken ct)
    {
        var existeCarrera = await _context.Carreras.AnyAsync(c => c.Id == carreraId, ct);
        if (!existeCarrera)
        {
            return NotFound();
        }

        var mallas = await _context.Mallas
            .Where(m => m.CarreraId == carreraId)
            .OrderByDescending(m => m.Vigente)
            .ThenBy(m => m.Version)
            .Select(m => new MallaCatalogoResponse(
                m.Id,
                m.CarreraId,
                m.Version,
                m.Vigente))
            .ToListAsync(ct);

        return Ok(mallas);
    }

    // GET: api/v1/catalogo/mallas/{mallaId}/niveles
    /// <summary>Lista los niveles de una versión de malla.</summary>
    /// <param name="mallaId">Identificador de la versión de malla.</param>
    /// <param name="ct">Token para cancelar la solicitud.</param>
    [HttpGet("mallas/{mallaId:guid}/niveles")]
    [ProducesResponseType(typeof(IReadOnlyList<NivelCatalogoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<NivelCatalogoResponse>>> GetNivelesPorMalla(
        Guid mallaId,
        CancellationToken ct)
    {
        var existeMalla = await _context.Mallas.AnyAsync(m => m.Id == mallaId, ct);
        if (!existeMalla)
        {
            return NotFound();
        }

        var niveles = await _context.Niveles
            .Where(n => n.MallaVersionId == mallaId)
            .OrderBy(n => n.Numero)
            .Select(n => new NivelCatalogoResponse(
                n.Id,
                n.MallaVersionId,
                n.Numero))
            .ToListAsync(ct);

        return Ok(niveles);
    }

    // GET: api/v1/catalogo/mallas/{mallaId}/asignaturas?nivelId=...&nivel=...&search=...
    /// <summary>Lista las asignaturas de una malla con filtros opcionales.</summary>
    /// <param name="mallaId">Identificador de la versión de malla.</param>
    /// <param name="nivelId">Filtra por el identificador del nivel.</param>
    /// <param name="nivel">Filtra por el número del nivel.</param>
    /// <param name="search">Busca por código, nombre o unidad organizacional, sin distinguir mayúsculas.</param>
    /// <param name="ct">Token para cancelar la solicitud.</param>
    [HttpGet("mallas/{mallaId:guid}/asignaturas")]
    [ProducesResponseType(typeof(IReadOnlyList<AsignaturaCatalogoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AsignaturaCatalogoResponse>>> GetAsignaturas(
        Guid mallaId,
        [FromQuery] Guid? nivelId,
        [FromQuery] int? nivel,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var existeMalla = await _context.Mallas.AnyAsync(m => m.Id == mallaId, ct);
        if (!existeMalla)
        {
            return NotFound();
        }

        var query = _context.Asignaturas
            .AsNoTracking()
            .Where(a => a.MallaVersionId == mallaId);

        if (nivelId.HasValue)
        {
            query = query.Where(a => a.NivelId == nivelId.Value);
        }

        if (nivel.HasValue)
        {
            query = query.Where(a => a.Nivel != null && a.Nivel.Numero == nivel.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Nombre.ToLower().Contains(term) ||
                a.Codigo.ToLower().Contains(term) ||
                (a.UnidadOrganizacional != null && a.UnidadOrganizacional.ToLower().Contains(term)));
        }

        var asignaturas = await query
            .OrderBy(a => a.Nivel != null ? a.Nivel.Numero : 0)
            .ThenBy(a => a.Codigo)
            .Select(a => new AsignaturaCatalogoResponse(
                a.Id,
                a.MallaVersionId,
                a.NivelId,
                a.Nivel != null ? a.Nivel.Numero : 0,
                a.Codigo,
                a.Nombre,
                a.Creditos,
                a.Horas,
                a.UnidadOrganizacional))
            .ToListAsync(ct);

        return Ok(asignaturas);
    }
}
