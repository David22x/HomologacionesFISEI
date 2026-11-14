using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Roles;
using HomologationService.Data;
using HomologationService.Models;
using HomologationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using static HomologationService.Services.DatosEvaluacionClient;

namespace HomologationService.Controllers;

[ApiController]
[Route("api/v1/homologacion/expedientes/{expedienteId:guid}")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public sealed class ParesExpedienteController(
    HomologationDbContext db, DatosEvaluacionClient datos) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    [HttpGet("pares")]
    public async Task<IActionResult> Listar(Guid expedienteId, CancellationToken ct)
    {
        var expediente = await BuscarAsync(expedienteId, ct);
        return expediente is null ? NotFound() : Ok(await ParesAsync(expedienteId, ct));
    }

    [HttpPost("pares/precargar")]
    [Authorize(Roles = AppRoles.Profesor)]
    public Task<IActionResult> Precargar(Guid expedienteId, CancellationToken ct) =>
        EditarAsync(expedienteId, async (expediente, bearer) =>
        {
            var historial = await datos.HistorialAsync(expediente.EstudianteId, bearer, ct);
            if (historial is null)
                return NotFound(new { mensaje = "El estudiante no tiene historial académico." });

            var destinos = await datos.DestinosAsync(expediente.MallaDestinoId, bearer, ct);
            var actuales = await ParesAsync(expedienteId, ct);
            var destinosPorNombre = destinos.ToLookup(x => x.Nombre.Trim(), StringComparer.OrdinalIgnoreCase);
            var origenesPorNombre = historial.Asignaturas.ToLookup(x => x.Nombre.Trim(), StringComparer.OrdinalIgnoreCase);
            var nuevos = new List<ParHomologacion>();
            foreach (var origen in origenesPorNombre)
            {
                var coincidencias = destinosPorNombre[origen.Key];
                if (origen.Key.Length == 0 || origen.Count() != 1 || coincidencias.Count() != 1)
                    continue;

                var materia = origen.Single();
                var destino = coincidencias.Single();
                if (actuales.Any(p => p.AsignaturaCursadaId == materia.Id || p.AsignaturaDestinoId == destino.Id))
                    continue;

                nuevos.Add(CrearPar(expedienteId, new(materia.Id, destino.Id), true));
            }

            if (nuevos.Count > 0)
            {
                db.Pares.AddRange(nuevos);
                await GuardarAsync(expediente, ct);
            }

            return Ok(actuales.Concat(nuevos));
        }, ct);

    [HttpPut("pares")]
    [Authorize(Roles = AppRoles.Profesor)]
    public Task<IActionResult> GuardarPares(Guid expedienteId, [FromBody] IReadOnlyList<ParSolicitud> pares, CancellationToken ct) =>
        EditarAsync(expedienteId, async (expediente, bearer) =>
        {
            if (pares.Any(p => p is null) || pares.Count != pares.Distinct().Count())
                return BadRequest(new { mensaje = "Envía pares válidos y sin duplicados." });

            if (pares.Count > 0)
            {
                var historial = await datos.HistorialAsync(expediente.EstudianteId, bearer, ct);
                var destinos = await datos.DestinosAsync(expediente.MallaDestinoId, bearer, ct);
                var origenIds = historial?.Asignaturas.Select(x => x.Id).ToHashSet() ?? [];
                var destinoIds = destinos.Select(x => x.Id).ToHashSet();
                if (pares.Any(p => !origenIds.Contains(p.AsignaturaCursadaId) || !destinoIds.Contains(p.AsignaturaDestinoId)))
                    return BadRequest(new { mensaje = "Cada par debe usar una materia del historial y una de la malla de destino." });
            }

            var actuales = (await ParesAsync(expedienteId, ct))
                .ToDictionary(p => new ParSolicitud(p.AsignaturaCursadaId, p.AsignaturaDestinoId));
            var solicitados = pares.ToHashSet();
            var eliminados = actuales.Where(p => !solicitados.Contains(p.Key)).Select(p => p.Value).ToArray();
            var nuevos = pares.Where(p => !actuales.ContainsKey(p)).Select(p => CrearPar(expedienteId, p, false)).ToArray();

            if (eliminados.Length > 0 || nuevos.Length > 0)
            {
                db.Pares.RemoveRange(eliminados);
                db.Pares.AddRange(nuevos);
                await GuardarAsync(expediente, ct);
            }

            return Ok(actuales.Values.Except(eliminados).Concat(nuevos));
        }, ct);

    [HttpPost("evaluar-reglas")]
    [Authorize(Roles = AppRoles.Profesor)]
    public Task<IActionResult> Evaluar(Guid expedienteId, CancellationToken ct) =>
        EditarAsync(expedienteId, async (expediente, bearer) =>
        {
            var pares = await ParesAsync(expedienteId, ct);
            if (pares.Count == 0)
                return BadRequest(new { mensaje = "Agrega al menos un par antes de evaluar." });

            var historial = await datos.HistorialAsync(expediente.EstudianteId, bearer, ct);
            var origenes = historial?.Asignaturas.ToDictionary(x => x.Id) ?? [];
            var destinos = (await datos.DestinosAsync(expediente.MallaDestinoId, bearer, ct)).ToDictionary(x => x.Id);
            if (pares.Any(p => !origenes.ContainsKey(p.AsignaturaCursadaId) || !destinos.ContainsKey(p.AsignaturaDestinoId)))
                return Conflict(new { mensaje = "El historial o la malla cambió. Actualiza los pares antes de evaluar." });

            var catalogoOrigen = new Dictionary<Guid, Asignatura?>();
            var contenidos = new Dictionary<Guid, Contenido[]>();
            var resultados = new List<ResultadoPar>();
            foreach (var par in pares)
            {
                var origen = origenes[par.AsignaturaCursadaId];
                var destino = destinos[par.AsignaturaDestinoId];
                Asignatura? fichaOrigen = null;
                Contenido[] contenidosOrigen = [];
                if (origen.AsignaturaId is Guid origenId)
                {
                    if (!catalogoOrigen.TryGetValue(origenId, out fichaOrigen))
                    {
                        fichaOrigen = await datos.AsignaturaAsync(origenId, bearer, ct);
                        catalogoOrigen.Add(origenId, fichaOrigen);
                    }

                    if (fichaOrigen is not null)
                        contenidosOrigen = await CargarContenidosAsync(origenId);
                }

                var solicitud = new SolicitudReglas(origen.Creditos ?? fichaOrigen?.Creditos, destino.Creditos,
                    origen.Horas ?? fichaOrigen?.Horas, destino.Horas, true, origen.Nota,
                    fichaOrigen?.EsTransversal ?? false, destino.EsTransversal,
                    contenidosOrigen, await CargarContenidosAsync(destino.Id));
                var resultado = await datos.EvaluarAsync(solicitud, bearer, ct);
                resultados.Add(new(par.Id, origen.Id, origen.Nombre, destino.Id, destino.Nombre, resultado));
            }

            var ejecucion = new EjecucionReglas
            {
                ExpedienteId = expedienteId,
                ProfesorId = expediente.ProfesorId,
                ResultadosJson = JsonSerializer.Serialize(resultados, JsonWeb)
            };
            db.EjecucionesReglas.Add(ejecucion);
            expediente.Estado = EstadoExpediente.EnRevision;
            await GuardarAsync(expediente, ct);

            return Ok(new { ejecucion.Id, ejecucion.EjecutadoEn, estado = expediente.Estado.ToString(), resultados });

            async Task<Contenido[]> CargarContenidosAsync(Guid asignaturaId)
            {
                if (contenidos.TryGetValue(asignaturaId, out var guardados))
                    return guardados;

                var temas = await datos.TemasAsync(asignaturaId, bearer, ct);
                var lista = new List<Contenido>(temas.Length);
                foreach (var tema in temas)
                {
                    var subtemas = await datos.SubtemasAsync(tema.Id, bearer, ct);
                    var descripcion = string.Join(" | ", subtemas.Select(x => x.Nombre).Prepend(tema.Nombre));
                    lista.Add(new(tema.Id.ToString(), descripcion, tema.HorasEstimadas));
                }

                var cargados = lista.ToArray();
                contenidos.Add(asignaturaId, cargados);
                return cargados;
            }
        }, ct);

    private async Task<IActionResult> EditarAsync(Guid id,
        Func<Expediente, AuthenticationHeaderValue, Task<IActionResult>> operacion, CancellationToken ct)
    {
        var expediente = await BuscarAsync(id, ct);
        if (expediente is null)
            return NotFound();

        if (expediente.Estado is not (EstadoExpediente.Borrador or EstadoExpediente.EnRevision))
            return Conflict(new { mensaje = "El estado del expediente impide modificar o evaluar sus pares." });

        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var bearer)
            || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        try
        {
            return await operacion(expediente, bearer);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { mensaje = "El expediente cambió durante la operación. Vuelve a consultarlo." });
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Conflict(new { mensaje = "Los pares cambiaron durante la operación. Vuelve a consultarlos." });
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
        {
            return Problem(statusCode: 503, detail: "Un servicio de evaluación no está disponible.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.GatewayTimeout)
        {
            return Problem(statusCode: 504, detail: "Un servicio de evaluación tardó demasiado.");
        }
        catch (HttpRequestException)
        {
            return Problem(statusCode: 502, detail: "Falló la comunicación con un servicio de evaluación.");
        }
        catch (JsonException)
        {
            return Problem(statusCode: 502, detail: "Un servicio devolvió datos de evaluación inválidos.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Problem(statusCode: 504, detail: "Un servicio de evaluación tardó demasiado.");
        }
    }

    private Task<Expediente?> BuscarAsync(Guid id, CancellationToken ct)
    {
        var usuario = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var profesorId = Guid.TryParse(usuario, out var valor) ? valor : Guid.Empty;
        var administrador = User.IsInRole(AppRoles.Administrador) && !User.IsInRole(AppRoles.Profesor);
        return db.Expedientes.SingleOrDefaultAsync(e => e.Id == id &&
            (administrador || (profesorId != Guid.Empty && e.ProfesorId == profesorId)), ct);
    }

    private Task<List<ParHomologacion>> ParesAsync(Guid id, CancellationToken ct) =>
        db.Pares.Where(p => p.ExpedienteId == id).OrderBy(p => p.Id).ToListAsync(ct);

    private Task GuardarAsync(Expediente expediente, CancellationToken ct)
    {
        expediente.ActualizadoEn = DateTime.UtcNow;
        return db.SaveChangesAsync(ct);
    }

    private static ParHomologacion CrearPar(Guid id, ParSolicitud par, bool precargado) =>
        new()
        {
            ExpedienteId = id,
            AsignaturaCursadaId = par.AsignaturaCursadaId,
            AsignaturaDestinoId = par.AsignaturaDestinoId,
            Precargado = precargado
        };

    public sealed record ParSolicitud(Guid AsignaturaCursadaId, Guid AsignaturaDestinoId);
    private sealed record ResultadoPar(Guid ParId, Guid AsignaturaCursadaId, string NombreOrigen,
        Guid AsignaturaDestinoId, string NombreDestino, JsonElement Evaluacion);
}
