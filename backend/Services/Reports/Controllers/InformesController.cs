using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReportsService.Data;
using ReportsService.Models;
using ReportsService.Services;

namespace ReportsService.Controllers;

[ApiController]
[Route("api/v1/informes/expedientes/{expedienteId:guid}")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public sealed class InformesController(ReportsDbContext db, FuenteInformeClient fuente) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    [HttpPost("generar")]
    [Authorize(Roles = AppRoles.Profesor)]
    public Task<IActionResult> Generar(Guid expedienteId, CancellationToken ct) =>
        EjecutarAsync(expedienteId, async (datos, bearer) =>
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor))
                return Unauthorized();
            var completos = await fuente.CompletarAsync(datos, bearer, ct);
            var json = JsonSerializer.Serialize(completos, JsonWeb);
            var huellaFuente = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json + "|plantilla:" + InformePdf.VersionPlantilla)));
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var existente = await db.Informes.AsNoTracking().Where(x =>
                x.ExpedienteId == expedienteId && x.HuellaFuente == huellaFuente)
                .Select(x => new { x.Id, x.Version, x.GeneradoEn, x.HuellaSha256 }).SingleOrDefaultAsync(ct);
            if (existente is not null)
            {
                await transaction.CommitAsync(ct);
                return Ok(existente);
            }
            var version = (await db.Informes.Where(x => x.ExpedienteId == expedienteId)
                .MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
            var informe = new Informe
            {
                ExpedienteId = expedienteId, Version = version,
                GeneradoPor = actor, DatosJson = json, HuellaFuente = huellaFuente
            };
            informe.Pdf = InformePdf.Generar(completos, version, informe.GeneradoEn);
            informe.HuellaSha256 = Convert.ToHexString(SHA256.HashData(informe.Pdf));
            db.Informes.Add(informe);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return CreatedAtAction(nameof(Descargar), new { expedienteId, version },
                new { informe.Id, informe.Version, informe.GeneradoEn, informe.HuellaSha256 });
        }, ct);

    [HttpGet]
    public Task<IActionResult> Versiones(Guid expedienteId, CancellationToken ct) =>
        EjecutarAsync(expedienteId, async (_, _) => Ok(await db.Informes.AsNoTracking()
            .Where(x => x.ExpedienteId == expedienteId).OrderByDescending(x => x.Version)
            .Select(x => new { x.Id, x.Version, x.GeneradoEn, x.GeneradoPor, x.HuellaSha256 })
            .ToListAsync(ct)), ct);

    [HttpGet("{version:int}/pdf")]
    public Task<IActionResult> Descargar(Guid expedienteId, int version, CancellationToken ct) =>
        EjecutarAsync(expedienteId, async (_, _) =>
        {
            var informe = await db.Informes.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ExpedienteId == expedienteId && x.Version == version, ct);
            if (informe is null) return NotFound();
            var huella = Convert.ToHexString(SHA256.HashData(informe.Pdf));
            if (huella != informe.HuellaSha256)
                return Problem(statusCode: 500, detail: "El PDF guardado no supera la verificación de integridad.");
            Response.Headers.ETag = "\"" + huella + "\"";
            Response.Headers["X-Content-SHA256"] = huella;
            Response.Headers.CacheControl = "private, no-store";
            return File(informe.Pdf, "application/pdf", $"homologacion-{expedienteId}-v{version}.pdf");
        }, ct);

    private async Task<IActionResult> EjecutarAsync(Guid id,
        Func<FuenteHomologacion, AuthenticationHeaderValue, Task<IActionResult>> accion, CancellationToken ct)
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var bearer)
            || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();
        try
        {
            var datos = await fuente.ConsultarAsync(id, bearer, ct);
            if (datos.Expediente.Id != id || datos.Expediente.Estado != 5 || datos.Decisiones.Length == 0)
                return Conflict(new { mensaje = "El informe requiere un expediente resuelto con decisiones." });
            return await accion(datos, bearer);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return NotFound(); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { return Unauthorized(); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden) { return Forbid(); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(new { mensaje = "Resuelve el expediente antes de emitir o consultar informes." });
        }
        catch (HttpRequestException) { return Problem(statusCode: 503, detail: "No se pudieron consultar los servicios académicos."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Problem(statusCode: 504, detail: "Un servicio académico tardó demasiado en responder.");
        }
        catch (JsonException) { return Problem(statusCode: 502, detail: "Los datos del informe no son válidos."); }
        catch (InvalidOperationException) { return Problem(statusCode: 502, detail: "Los datos del informe están incompletos."); }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "Se generó un informe concurrentemente. Reintenta para recuperar su versión." });
        }
        catch (SqlException ex) when (ex.Number is 1205 or 2601 or 2627)
        {
            return Conflict(new { mensaje = "Otra generación está en curso. Reintenta la consulta." });
        }
    }
}
