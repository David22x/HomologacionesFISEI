using BuildingBlocks.AcademicImports;
using BuildingBlocks.Roles;
using HomologationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HomologationService.Controllers;

[ApiController]
[Route("api/v1/agent-ia/extraer-record")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public sealed class RecordsController(ExtractorRecordIa extractor) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Extraer(SolicitudExtraccionRecord solicitud, CancellationToken ct)
    {
        try { return Ok(await extractor.ExtraerAsync(solicitud.Texto, ct)); }
        catch (InvalidDataException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return StatusCode(502, new { mensaje = "La respuesta de IA no es válida. Revisa el documento o completa los datos manualmente." }); }
        catch (HttpRequestException) { return StatusCode(503, new { mensaje = "Ollama no está disponible. Reintenta o completa los datos manualmente." }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return StatusCode(504, new { mensaje = "La extracción agotó su tiempo. Divide el documento o completa los datos manualmente." }); }
    }
}
