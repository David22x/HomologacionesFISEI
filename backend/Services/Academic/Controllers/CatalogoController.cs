using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcademicService.Data;
using BuildingBlocks.Roles;

namespace AcademicService.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public class CatalogoController : ControllerBase
{
    private readonly AcademicDbContext _context;

    public CatalogoController(AcademicDbContext context)
    {
        _context = context;
    }

    // GET: api/v1/catalogo/carreras
    [HttpGet("carreras")]
    public async Task<IActionResult> GetCarreras(CancellationToken ct)
    {
        var carreras = await _context.Carreras
            .OrderBy(c => c.EsDemo)
            .ThenBy(c => c.Nombre)
            .Select(c => new
            {
                c.Id,
                c.Codigo,
                c.Nombre,
                c.EsDemo
            })
            .ToListAsync(ct);

        return Ok(carreras);
    }

    // GET: api/v1/catalogo/carreras/{carreraId}/mallas
    [HttpGet("carreras/{carreraId:guid}/mallas")]
    public async Task<IActionResult> GetMallasPorCarrera(Guid carreraId, CancellationToken ct)
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
            .Select(m => new
            {
                m.Id,
                m.CarreraId,
                m.Version,
                m.Vigente
            })
            .ToListAsync(ct);

        return Ok(mallas);
    }

    // GET: api/v1/catalogo/mallas/{mallaId}/niveles
    [HttpGet("mallas/{mallaId:guid}/niveles")]
    public async Task<IActionResult> GetNivelesPorMalla(Guid mallaId, CancellationToken ct)
    {
        var existeMalla = await _context.Mallas.AnyAsync(m => m.Id == mallaId, ct);
        if (!existeMalla)
        {
            return NotFound();
        }

        var niveles = await _context.Niveles
            .Where(n => n.MallaVersionId == mallaId)
            .OrderBy(n => n.Numero)
            .Select(n => new
            {
                n.Id,
                n.MallaVersionId,
                n.Numero
            })
            .ToListAsync(ct);

        return Ok(niveles);
    }

    // GET: api/v1/catalogo/mallas/{mallaId}/asignaturas?nivelId=...&nivel=...&search=...
    [HttpGet("mallas/{mallaId:guid}/asignaturas")]
    public async Task<IActionResult> GetAsignaturas(
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
            .Select(a => new
            {
                a.Id,
                a.MallaVersionId,
                a.NivelId,
                NivelNumero = a.Nivel != null ? a.Nivel.Numero : 0,
                a.Codigo,
                a.Nombre,
                a.Creditos,
                a.Horas,
                a.UnidadOrganizacional
            })
            .ToListAsync(ct);

        return Ok(asignaturas);
    }
}