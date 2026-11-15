using BuildingBlocks.Roles;
using HomologationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomologationService.Controllers;

[ApiController]
[Route("api/v1/homologacion/evaluaciones")]
[Route("api/v1/agent-ia/evaluar-reglas")]
[Authorize(Roles = AppRoles.Profesor)]
public sealed class EvaluacionesController(
    IAgenteIaService agenteIa) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Evaluar(
        [FromBody] SolicitudEvaluacionIa solicitud,
        CancellationToken cancellationToken)
    {
        if (solicitud.ContenidosOrigen is null
            || solicitud.ContenidosDestino is null)
        {
            return BadRequest(new
            {
                mensaje = "Debes enviar contenidos de origen y de destino."
            });
        }

        try
        {
            var resultado = await agenteIa.EvaluarAsync(
                solicitud, cancellationToken);

            return Ok(resultado);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            return StatusCode(503, new
            {
                mensaje = "Agente IA alcanzó temporalmente su límite gratuito. Intenta más tarde."
            });
        }
        catch (HttpRequestException ex) when (
        ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            return StatusCode(503, new
            {
                mensaje = "Agente IA está temporalmente saturado. Espera un poco e intenta de nuevo."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new
            {
                mensaje = "Agente IA tardó demasiado en responder. Intenta más tarde."
            });
        }
        catch (HttpRequestException ex)
        {
            var codigoProveedor = ex.StatusCode is null
                ? (int?)null
                : (int)ex.StatusCode.Value;

            return StatusCode(502, new
            {
                mensaje = "Falló la solicitud al Agente IA.",
                codigoProveedor
            });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(503, new
            {
                mensaje = "Revisa la configuración de la clave del Agente IA."
            });
        }
    }
}
