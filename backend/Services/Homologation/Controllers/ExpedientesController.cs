using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using HomologationService.Data; 
using HomologationService.Models; 
using BuildingBlocks.Roles;

namespace HomologationService.Controllers;

[ApiController]
[Route("api/v1/homologacion/[controller]")]
[Authorize]
public class ExpedientesController : ControllerBase
{
    private readonly HomologationDbContext _context;

    public ExpedientesController(HomologationDbContext context)
    {
        _context = context;
    }
    
    [HttpPost]
    [Authorize(Roles = AppRoles.Profesor)]
    public IActionResult CrearExpediente([FromBody] CrearExpedienteDto request)
    {
        var profesorIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(profesorIdStr)) return Unauthorized();

        Guid profesorId = Guid.Parse(profesorIdStr);

        // Regla: No pueden existir dos expedientes activos para el mismo estudiante y carrera.
        bool existeActivo = _context.Expedientes.Any(e => 
            e.EstudianteId == request.EstudianteRefId && 
            e.CarreraDestinoId == request.CarreraDestinoRefId && 
            e.Estado != EstadoExpediente.Finalizado && 
            e.Estado != EstadoExpediente.Anulado);
            
        if (existeActivo)
            return BadRequest(new { Mensaje = "Ya existe un expediente activo para este estudiante y carrera." });

        var nuevoExpediente = new Expediente 
        {
            Id = Guid.NewGuid(),
            EstudianteId = request.EstudianteRefId, 
            CarreraDestinoId = request.CarreraDestinoRefId,
            MallaDestinoId = request.MallaAplicableRefId,
            ProfesorId = profesorId,
            Estado = EstadoExpediente.Borrador, 
            CreadoEn = DateTime.UtcNow
        };

        _context.Expedientes.Add(nuevoExpediente);
        _context.SaveChanges();

        return CreatedAtAction(nameof(ConsultarExpedientes), new { id = nuevoExpediente.Id }, nuevoExpediente);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
    public IActionResult ConsultarExpedientes()
    {
        var rol = User.FindFirst(ClaimTypes.Role)?.Value;
        var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var query = _context.Expedientes.AsQueryable();

        // Regla: El Profesor ve solo los expedientes que le corresponden. El Admin ve todos.
        if (rol == AppRoles.Profesor && !string.IsNullOrEmpty(usuarioIdStr))
        {
            Guid usuarioId = Guid.Parse(usuarioIdStr);
            query = query.Where(e => e.ProfesorId == usuarioId); 
        }

        var expedientes = query.ToList();
        
        return Ok(expedientes);
    }
}

public class CrearExpedienteDto
{
    public Guid EstudianteRefId { get; set; }
    public Guid CarreraDestinoRefId { get; set; }
    public Guid MallaAplicableRefId { get; set; }
}