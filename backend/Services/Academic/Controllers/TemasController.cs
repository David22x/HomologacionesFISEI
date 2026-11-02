using AcademicService.Contracts;
using AcademicService.Data;
using AcademicService.Models;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AcademicService.Controllers;

[ApiController]
[Route("api/v1/catalogo")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public sealed class TemasController(AcademicDbContext context) : ControllerBase
{
    /// <summary>Lista los temas de una asignatura ordenados por su posición.</summary>
    [HttpGet("asignaturas/{asignaturaId:guid}/temas")]
    [ProducesResponseType(typeof(IReadOnlyList<TemaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TemaResponse>>> GetTemasPorAsignatura(
        Guid asignaturaId,
        CancellationToken ct)
    {
        if (!await context.Asignaturas.AnyAsync(a => a.Id == asignaturaId, ct))
        {
            return NotFound();
        }

        var temas = await context.Temas
            .AsNoTracking()
            .Where(t => t.AsignaturaId == asignaturaId)
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.Nombre)
            .Select(t => new TemaResponse(
                t.Id, t.AsignaturaId, t.Nombre, t.Orden, t.HorasEstimadas))
            .ToListAsync(ct);

        return Ok(temas);
    }

    /// <summary>Obtiene un tema por su identificador.</summary>
    [HttpGet("temas/{temaId:guid}")]
    [ProducesResponseType(typeof(TemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemaResponse>> GetTema(Guid temaId, CancellationToken ct)
    {
        var tema = await context.Temas.AsNoTracking()
            .Where(t => t.Id == temaId)
            .Select(t => new TemaResponse(
                t.Id, t.AsignaturaId, t.Nombre, t.Orden, t.HorasEstimadas))
            .FirstOrDefaultAsync(ct);

        return tema is null ? NotFound() : Ok(tema);
    }

    /// <summary>Crea un tema en una asignatura. Solo Administrador.</summary>
    [HttpPost("asignaturas/{asignaturaId:guid}/temas")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(TemaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TemaResponse>> CrearTema(
        Guid asignaturaId,
        CreateTemaRequest request,
        CancellationToken ct)
    {
        if (!await context.Asignaturas.AnyAsync(a => a.Id == asignaturaId, ct))
        {
            return NotFound();
        }

        var error = ValidarTema(request.Nombre, request.Orden, request.HorasEstimadas);
        if (error is not null)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest, error));
        }

        var nombre = request.Nombre.Trim();
        if (await context.Temas.AnyAsync(
                t => t.AsignaturaId == asignaturaId && t.Nombre.ToUpper() == nombre.ToUpper(), ct))
        {
            return Conflicto("Ya existe un tema con ese nombre en la asignatura.");
        }

        var tema = new Tema
        {
            AsignaturaId = asignaturaId,
            Nombre = nombre,
            Orden = request.Orden,
            HorasEstimadas = request.HorasEstimadas
        };
        context.Temas.Add(tema);
        if (!await GuardarTemaAsync(ct))
        {
            return Conflicto("Ya existe un tema con ese nombre en la asignatura.");
        }

        return CreatedAtAction(nameof(GetTema), new { temaId = tema.Id }, ToResponse(tema));
    }

    /// <summary>Actualiza un tema existente. Solo Administrador.</summary>
    [HttpPut("temas/{temaId:guid}")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(TemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TemaResponse>> ActualizarTema(
        Guid temaId,
        UpdateTemaRequest request,
        CancellationToken ct)
    {
        var tema = await context.Temas.FirstOrDefaultAsync(t => t.Id == temaId, ct);
        if (tema is null)
        {
            return NotFound();
        }

        var error = ValidarTema(request.Nombre, request.Orden, request.HorasEstimadas);
        if (error is not null)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest, error));
        }

        var nombre = request.Nombre.Trim();
        if (await context.Temas.AnyAsync(
                t => t.Id != temaId && t.AsignaturaId == tema.AsignaturaId
                    && t.Nombre.ToUpper() == nombre.ToUpper(), ct))
        {
            return Conflicto("Ya existe un tema con ese nombre en la asignatura.");
        }

        tema.Nombre = nombre;
        tema.Orden = request.Orden;
        tema.HorasEstimadas = request.HorasEstimadas;
        if (!await GuardarTemaAsync(ct))
        {
            return Conflicto("Ya existe un tema con ese nombre en la asignatura.");
        }

        return Ok(ToResponse(tema));
    }

    /// <summary>Elimina un tema y sus subtemas. Solo Administrador.</summary>
    [HttpDelete("temas/{temaId:guid}")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarTema(Guid temaId, CancellationToken ct)
    {
        var tema = await context.Temas.FirstOrDefaultAsync(t => t.Id == temaId, ct);
        if (tema is null)
        {
            return NotFound();
        }

        context.Temas.Remove(tema);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Lista los subtemas de un tema ordenados por su posición.</summary>
    [HttpGet("temas/{temaId:guid}/subtemas")]
    [ProducesResponseType(typeof(IReadOnlyList<SubtemaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SubtemaResponse>>> GetSubtemasPorTema(
        Guid temaId,
        CancellationToken ct)
    {
        if (!await context.Temas.AnyAsync(t => t.Id == temaId, ct))
        {
            return NotFound();
        }

        var subtemas = await context.Subtemas
            .AsNoTracking()
            .Where(s => s.TemaId == temaId)
            .OrderBy(s => s.Orden)
            .ThenBy(s => s.Nombre)
            .Select(s => new SubtemaResponse(
                s.Id, s.TemaId, s.Nombre, s.Orden, s.HorasEstimadas))
            .ToListAsync(ct);

        return Ok(subtemas);
    }

    /// <summary>Obtiene un subtema por su identificador.</summary>
    [HttpGet("subtemas/{subtemaId:guid}")]
    [ProducesResponseType(typeof(SubtemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubtemaResponse>> GetSubtema(Guid subtemaId, CancellationToken ct)
    {
        var subtema = await context.Subtemas.AsNoTracking()
            .Where(s => s.Id == subtemaId)
            .Select(s => new SubtemaResponse(
                s.Id, s.TemaId, s.Nombre, s.Orden, s.HorasEstimadas))
            .FirstOrDefaultAsync(ct);

        return subtema is null ? NotFound() : Ok(subtema);
    }

    /// <summary>Crea un subtema dentro de un tema. Solo Administrador.</summary>
    [HttpPost("temas/{temaId:guid}/subtemas")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(SubtemaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubtemaResponse>> CrearSubtema(
        Guid temaId,
        CreateSubtemaRequest request,
        CancellationToken ct)
    {
        if (!await context.Temas.AnyAsync(t => t.Id == temaId, ct))
        {
            return NotFound();
        }

        var error = ValidarTema(request.Nombre, request.Orden, request.HorasEstimadas);
        if (error is not null)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest, error));
        }

        var nombre = request.Nombre.Trim();
        if (await context.Subtemas.AnyAsync(
                s => s.TemaId == temaId && s.Nombre.ToUpper() == nombre.ToUpper(), ct))
        {
            return Conflicto("Ya existe un subtema con ese nombre en el tema.");
        }

        var subtema = new Subtema
        {
            TemaId = temaId,
            Nombre = nombre,
            Orden = request.Orden,
            HorasEstimadas = request.HorasEstimadas
        };
        context.Subtemas.Add(subtema);
        if (!await GuardarTemaAsync(ct))
        {
            return Conflicto("Ya existe un subtema con ese nombre en el tema.");
        }

        return CreatedAtAction(nameof(GetSubtema), new { subtemaId = subtema.Id }, ToResponse(subtema));
    }

    /// <summary>Actualiza un subtema existente. Solo Administrador.</summary>
    [HttpPut("subtemas/{subtemaId:guid}")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(SubtemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubtemaResponse>> ActualizarSubtema(
        Guid subtemaId,
        UpdateSubtemaRequest request,
        CancellationToken ct)
    {
        var subtema = await context.Subtemas.FirstOrDefaultAsync(s => s.Id == subtemaId, ct);
        if (subtema is null)
        {
            return NotFound();
        }

        var error = ValidarTema(request.Nombre, request.Orden, request.HorasEstimadas);
        if (error is not null)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest, error));
        }

        var nombre = request.Nombre.Trim();
        if (await context.Subtemas.AnyAsync(
                s => s.Id != subtemaId && s.TemaId == subtema.TemaId
                    && s.Nombre.ToUpper() == nombre.ToUpper(), ct))
        {
            return Conflicto("Ya existe un subtema con ese nombre en el tema.");
        }

        subtema.Nombre = nombre;
        subtema.Orden = request.Orden;
        subtema.HorasEstimadas = request.HorasEstimadas;
        if (!await GuardarTemaAsync(ct))
        {
            return Conflicto("Ya existe un subtema con ese nombre en el tema.");
        }

        return Ok(ToResponse(subtema));
    }

    /// <summary>Elimina un subtema. Solo Administrador.</summary>
    [HttpDelete("subtemas/{subtemaId:guid}")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarSubtema(Guid subtemaId, CancellationToken ct)
    {
        var subtema = await context.Subtemas.FirstOrDefaultAsync(s => s.Id == subtemaId, ct);
        if (subtema is null)
        {
            return NotFound();
        }

        context.Subtemas.Remove(subtema);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> GuardarTemaAsync(CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
        {
            return false;
        }
    }

    private static bool EsViolacionDeUnicidad(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException
                && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ValidarTema(string nombre, int orden, int? horasEstimadas)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "El nombre es obligatorio.";
        }

        if (nombre.Trim().Length > 300)
        {
            return "El nombre no puede superar los 300 caracteres.";
        }

        if (orden <= 0)
        {
            return "El orden debe ser mayor que cero.";
        }

        if (horasEstimadas is <= 0)
        {
            return "Las horas estimadas deben ser mayores que cero.";
        }

        return null;
    }

    private ConflictObjectResult Conflicto(string detail) =>
        Conflict(Problema(StatusCodes.Status409Conflict, detail));

    private static ProblemDetails Problema(int status, string detail) => new()
    {
        Status = status,
        Title = status == StatusCodes.Status409Conflict ? "Conflicto de datos" : "Solicitud no válida",
        Detail = detail
    };

    private static TemaResponse ToResponse(Tema tema) =>
        new(tema.Id, tema.AsignaturaId, tema.Nombre, tema.Orden, tema.HorasEstimadas);

    private static SubtemaResponse ToResponse(Subtema subtema) =>
        new(subtema.Id, subtema.TemaId, subtema.Nombre, subtema.Orden, subtema.HorasEstimadas);
}
