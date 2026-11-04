using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using HomologationService.Data; 
using HomologationService.Models; 
using BuildingBlocks.Roles;
using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HomologationService.Controllers;

[ApiController]
[Route("api/v1/homologacion/[controller]")]
[Authorize]
public class ExpedientesController : ControllerBase
{
    private readonly HomologationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public ExpedientesController(HomologationDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }
    
    [HttpPost]
    [Authorize(Roles = AppRoles.Profesor)]
    public async Task<IActionResult> CrearExpediente(
        [FromBody] CrearExpedienteDto request, CancellationToken ct)
    {
        if (request.EstudianteRefId == Guid.Empty || request.CarreraDestinoRefId == Guid.Empty
            || request.MallaAplicableRefId == Guid.Empty)
            return BadRequest(new { mensaje = "Estudiante, carrera de destino y malla son obligatorios." });

        var profesorIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(profesorIdStr)) return Unauthorized();
        if (!Guid.TryParse(profesorIdStr, out var profesorId)) return Unauthorized();

        var authorization = Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var bearer)
            || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        try
        {
            using var studentRequest = new HttpRequestMessage(
                HttpMethod.Get, $"/api/v1/estudiantes/{request.EstudianteRefId}");
            studentRequest.Headers.Authorization = bearer;
            using var studentResponse = await _httpClientFactory.CreateClient("StudentsService").SendAsync(studentRequest, ct);
            if (studentResponse.StatusCode == HttpStatusCode.NotFound)
                return NotFound(new { mensaje = "El estudiante seleccionado no existe." });
            if (!studentResponse.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { mensaje = "No se pudo validar el estudiante. Intenta nuevamente." });

            using var careerRequest = new HttpRequestMessage(HttpMethod.Get,
                $"/api/v1/catalogo/carreras/{request.CarreraDestinoRefId}/mallas");
            careerRequest.Headers.Authorization = bearer;
            using var careerResponse = await _httpClientFactory.CreateClient("AcademicService").SendAsync(careerRequest, ct);
            if (careerResponse.StatusCode == HttpStatusCode.NotFound)
                return NotFound(new { mensaje = "La carrera de destino seleccionada no existe." });
            if (!careerResponse.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { mensaje = "No se pudo validar la carrera y la malla. Intenta nuevamente." });

            using var mallasDocument = await JsonDocument.ParseAsync(
                await careerResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (mallasDocument.RootElement.ValueKind != JsonValueKind.Array
                || !mallasDocument.RootElement.EnumerateArray().Any(item =>
                    item.TryGetProperty("id", out var id) && id.TryGetGuid(out var value)
                    && value == request.MallaAplicableRefId))
                return BadRequest(new { mensaje = "La malla no pertenece a la carrera de destino seleccionada." });
        }
        catch (JsonException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { mensaje = "Academic Service devolvió una respuesta no válida." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { mensaje = "No fue posible comunicarse con los servicios académicos. Intenta nuevamente." });
        }

        // Regla: No pueden existir dos expedientes activos para el mismo estudiante y carrera.
        bool existeActivo = await _context.Expedientes.AnyAsync(e =>
            e.EstudianteId == request.EstudianteRefId && 
            e.CarreraDestinoId == request.CarreraDestinoRefId && 
            e.Estado != EstadoExpediente.Finalizado && 
            e.Estado != EstadoExpediente.Anulado, ct);
            
        if (existeActivo)
            return BadRequest(new { Mensaje = "Ya existe un expediente activo para este estudiante y carrera." });

        var nuevoExpediente = new Expediente 
        {
            Id = Guid.NewGuid(),
            EstudianteId = request.EstudianteRefId, 
            CarreraDestinoId = request.CarreraDestinoRefId,
            MallaDestinoId = request.MallaAplicableRefId,
            ProfesorId = profesorId,
            ProfesorNombre = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
            Estado = EstadoExpediente.Borrador, 
            CreadoEn = DateTime.UtcNow
        };

        _context.Expedientes.Add(nuevoExpediente);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ConsultarExpedientes), new { id = nuevoExpediente.Id }, nuevoExpediente);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
    public async Task<IActionResult> ConsultarExpedientes(CancellationToken ct)
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

        var expedientes = await query.OrderByDescending(e => e.CreadoEn).ToListAsync(ct);
        
        return Ok(expedientes);
    }
}

public class CrearExpedienteDto
{
    public Guid EstudianteRefId { get; set; }
    public Guid CarreraDestinoRefId { get; set; }
    public Guid MallaAplicableRefId { get; set; }
}
