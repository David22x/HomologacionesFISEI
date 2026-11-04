using System.Security.Claims;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsService.Contracts;
using StudentsService.Data;
using StudentsService.Models;

namespace StudentsService.Controllers;

[ApiController]
[Route("api/v1/expedientes")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public sealed class ExpedientesController(StudentsDbContext context) : ControllerBase
{
    private static readonly HashSet<string> EstadosPermitidos =
        ["Pendiente", "En revisión", "Aprobado", "Rechazado"];

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExpedienteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExpedienteResponse>>> Listar(CancellationToken ct)
    {
        var expedientes = await context.Expedientes.AsNoTracking()
            .Include(item => item.Estudiante)
            .OrderByDescending(item => item.CreadoEn)
            .ToListAsync(ct);
        return Ok(expedientes.Select(ToResponse).ToList());
    }

    [HttpGet("{expedienteId:guid}")]
    [ProducesResponseType(typeof(ExpedienteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpedienteResponse>> Obtener(Guid expedienteId, CancellationToken ct)
    {
        var expediente = await context.Expedientes.AsNoTracking()
            .Include(item => item.Estudiante)
            .FirstOrDefaultAsync(item => item.Id == expedienteId, ct);
        return expediente is null ? NotFound() : Ok(ToResponse(expediente));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExpedienteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpedienteResponse>> Crear(
        ExpedienteCreateRequest request,
        CancellationToken ct)
    {
        if (request.EstudianteId == Guid.Empty || request.CarreraId == Guid.Empty || request.MallaId == Guid.Empty)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Solicitud inválida",
                Detail = "Selecciona un estudiante, una carrera y una malla válidos."
            });

        var student = await context.Estudiantes
            .FirstOrDefaultAsync(item => item.Id == request.EstudianteId, ct);
        if (student is null) return NotFound();

        var expediente = new Expediente
        {
            Estudiante = student,
            CarreraId = request.CarreraId,
            MallaId = request.MallaId,
            CreadoPor = ActorActual(),
            ModificadoPor = ActorActual()
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obtener), new { expedienteId = expediente.Id },
            ToResponse(expediente));
    }

    [HttpPut("{expedienteId:guid}/estado")]
    [ProducesResponseType(typeof(ExpedienteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpedienteResponse>> ActualizarEstado(
        Guid expedienteId,
        ExpedienteEstadoRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Estado) || !EstadosPermitidos.Contains(request.Estado))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Solicitud inválida",
                Detail = "El estado debe ser Pendiente, En revisión, Aprobado o Rechazado."
            });

        var expediente = await context.Expedientes.Include(item => item.Estudiante)
            .FirstOrDefaultAsync(item => item.Id == expedienteId, ct);
        if (expediente is null) return NotFound();

        expediente.Estado = request.Estado;
        expediente.ModificadoPor = ActorActual();
        expediente.ModificadoEn = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return Ok(ToResponse(expediente));
    }

    private string ActorActual() =>
        User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "desconocido";

    private static ExpedienteResponse ToResponse(Expediente item) => new(
        item.Id,
        item.EstudianteId,
        item.Estudiante.Cedula,
        item.Estudiante.Nombres,
        item.Estudiante.Apellidos,
        item.CarreraId,
        item.MallaId,
        item.Estado,
        item.CreadoPor,
        item.CreadoEn,
        item.ModificadoPor,
        item.ModificadoEn);
}
