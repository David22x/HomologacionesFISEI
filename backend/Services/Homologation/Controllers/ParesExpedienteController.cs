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

                var cargados = await datos.ContenidosAsync(asignaturaId, bearer, ct);
                contenidos.Add(asignaturaId, cargados);
                return cargados;
            }
        }, ct);

    [HttpPost("analizar-ia")]
    [Authorize(Roles = AppRoles.Profesor)]
    public Task<IActionResult> AnalizarIa(Guid expedienteId, CancellationToken ct) =>
        EditarAsync(expedienteId, async (expediente, bearer) =>
        {
            var ejecucion = await db.EjecucionesReglas.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ExpedienteId == expedienteId && x.EsAnalisisIa, ct);
            if (ejecucion is null)
            {
                var analisis = await new GeneradorPropuestas(datos).GenerarAsync(expediente, bearer, ct);
                ejecucion = new EjecucionReglas
                {
                    ExpedienteId = expedienteId,
                    ProfesorId = expediente.ProfesorId,
                    EsAnalisisIa = true,
                    ResultadosJson = JsonSerializer.Serialize(analisis, JsonWeb)
                };
                db.EjecucionesReglas.Add(ejecucion);
                expediente.Estado = EstadoExpediente.AnalizadoConIa;
                await GuardarAsync(expediente, ct);
            }

            var resultado = JsonSerializer.Deserialize<GeneradorPropuestas.Analisis>(ejecucion.ResultadosJson, JsonWeb)!;
            return Ok(new
            {
                ejecucion.Id,
                ejecutadoEn = DateTime.SpecifyKind(ejecucion.EjecutadoEn, DateTimeKind.Utc),
                estado = expediente.Estado.ToString(),
                resultado.Propuestas,
                resultado.Advertencias
            });
        }, ct);

    [HttpPost("analisis-ia/propuestas/agrupar")]
    [Authorize(Roles = AppRoles.Profesor)]
    public Task<IActionResult> AgruparOrigenes(Guid expedienteId,
        [FromBody] AgruparOrigenesDto request, CancellationToken ct) =>
        EditarAsync(expedienteId, async (expediente, bearer) =>
        {
            if (request.AsignaturaDestinoId == Guid.Empty || request.OrigenIds is null
                || request.OrigenIds.Count < 2 || request.OrigenIds.Any(id => id == Guid.Empty)
                || request.OrigenIds.Distinct().Count() != request.OrigenIds.Count)
                return BadRequest(new { mensaje = "Selecciona un destino y al menos dos orígenes distintos." });

            var ejecucion = await db.EjecucionesReglas.SingleOrDefaultAsync(
                x => x.ExpedienteId == expedienteId && x.EsAnalisisIa, ct);
            if (ejecucion is null)
                return Conflict(new { mensaje = "Primero genera el análisis con IA." });

            var analisis = JsonSerializer.Deserialize<GeneradorPropuestas.Analisis>(ejecucion.ResultadosJson, JsonWeb);
            if (analisis is null)
                return Problem(statusCode: 500, detail: "No se pudo leer el análisis guardado.");

            var origenes = request.OrigenIds.Order().ToArray();
            var existente = analisis.Propuestas.FirstOrDefault(propuesta =>
                propuesta.AsignaturaDestinoId == request.AsignaturaDestinoId
                && propuesta.Origenes.Select(origen => origen.AsignaturaCursadaId).Order().SequenceEqual(origenes));
            if (existente is not null)
                return Ok(existente);

            var propuesta = await new GeneradorPropuestas(datos).GenerarGrupoAsync(
                expediente, request.AsignaturaDestinoId, origenes, bearer, ct);
            if (propuesta is null)
                return BadRequest(new { mensaje = "El destino o los orígenes no pertenecen al expediente, o el grupo repite una asignatura de catálogo." });

            ejecucion.ResultadosJson = JsonSerializer.Serialize(
                analisis with { Propuestas = analisis.Propuestas.Append(propuesta).ToArray() }, JsonWeb);
            expediente.Estado = EstadoExpediente.AnalizadoConIa;
            await GuardarAsync(expediente, ct);
            return Ok(propuesta);
        }, ct);

    [HttpPut("analisis-ia/propuestas/revision")]
    [Authorize(Roles = AppRoles.Profesor)]
    public Task<IActionResult> RevisarPropuestaIa(Guid expedienteId,
        [FromBody] RevisionPropuestaIaDto request, CancellationToken ct) =>
        EditarAsync(expedienteId, async (expediente, _) =>
        {
            if (request.AsignaturaDestinoId == Guid.Empty
                || request.OrigenIds is null
                || request.OrigenIds.Count == 0
                || request.OrigenIds.Any(id => id == Guid.Empty)
                || request.OrigenIds.Distinct().Count() != request.OrigenIds.Count)
                return BadRequest(new { mensaje = "Selecciona una propuesta válida para revisar." });

            if (request.AceptarPropuestaIa == request.ResultadoPropio.HasValue)
                return BadRequest(new { mensaje = "Acepta la recomendación de IA o indica una conclusión propia, pero no ambas." });

            var observacion = request.Observacion?.Trim();
            if (observacion?.Length > 2000)
                return BadRequest(new { mensaje = "La observación no puede superar 2000 caracteres." });
            if (!request.AceptarPropuestaIa && string.IsNullOrWhiteSpace(observacion))
                return BadRequest(new { mensaje = "Explica brevemente el motivo de tu conclusión." });

            if (request.ResultadoPropio is int resultadoPropio
                && resultadoPropio is < 0 or > 5)
                return BadRequest(new { mensaje = "La conclusión seleccionada no es válida." });

            var ejecucion = await db.EjecucionesReglas.SingleOrDefaultAsync(
                x => x.ExpedienteId == expedienteId && x.EsAnalisisIa, ct);
            if (ejecucion is null)
                return NotFound(new { mensaje = "Primero genera el análisis con IA." });

            var analisis = JsonSerializer.Deserialize<GeneradorPropuestas.Analisis>(ejecucion.ResultadosJson, JsonWeb);
            if (analisis is null)
                return Problem(statusCode: 500, detail: "No se pudo leer el análisis guardado.");

            var origenesSolicitados = request.OrigenIds.Order().ToArray();
            var propuestas = analisis.Propuestas.ToArray();
            var indice = Array.FindIndex(propuestas, propuesta =>
                propuesta.AsignaturaDestinoId == request.AsignaturaDestinoId
                && propuesta.Origenes.Select(origen => origen.AsignaturaCursadaId).Order()
                    .SequenceEqual(origenesSolicitados));
            if (indice < 0)
                return NotFound(new { mensaje = "La propuesta ya no forma parte del análisis guardado." });

            int resultadoFinal;
            if (request.AceptarPropuestaIa)
            {
                var reglas = propuestas[indice].Evaluacion.GetProperty("reglas");
                var valorResultado = reglas.GetProperty("resultado");
                if (valorResultado.ValueKind != JsonValueKind.Number
                    || !valorResultado.TryGetInt32(out resultadoFinal)
                    || resultadoFinal is < 0 or > 5)
                    return Problem(statusCode: 502, detail: "La propuesta de IA no contiene un resultado de reglas válido.");
            }
            else
            {
                resultadoFinal = request.ResultadoPropio!.Value;
            }

            var resultadoCalculado = propuestas[indice].Evaluacion.GetProperty("reglas").GetProperty("resultado").GetInt32();
            if ((resultadoFinal != resultadoCalculado || resultadoFinal is not (0 or 4))
                && string.IsNullOrWhiteSpace(observacion))
                return BadRequest(new { mensaje = "La modificación, el rechazo o los datos incompletos requieren una justificación." });

            var vigentes = await DecisionesVigentesAsync(expedienteId, ct);
            if (resultadoFinal is 0 or 4 && vigentes.Any(d =>
                d.AsignaturaDestinoId != request.AsignaturaDestinoId && d.ResultadoFinal is 0 or 4
                && JsonSerializer.Deserialize<Guid[]>(d.OrigenIdsJson)!.Intersect(origenesSolicitados).Any()))
                return Conflict(new { mensaje = "Una asignatura de origen ya respalda otro destino aceptado." });

            db.Decisiones.Add(new Decision
            {
                ExpedienteId = expedienteId,
                AsignaturaDestinoId = request.AsignaturaDestinoId,
                OrigenIdsJson = JsonSerializer.Serialize(origenesSolicitados),
                ResultadoCalculado = resultadoCalculado,
                ResultadoFinal = resultadoFinal,
                Justificacion = string.IsNullOrWhiteSpace(observacion) ? null : observacion,
                ProfesorId = expediente.ProfesorId
            });

            var profesorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var revision = new GeneradorPropuestas.RevisionDocentePropuesta(
                request.AceptarPropuestaIa ? "AceptaPropuestaIa" : "ConclusionPropia",
                resultadoFinal,
                string.IsNullOrWhiteSpace(observacion) ? null : observacion,
                DateTime.UtcNow,
                profesorId,
                User.FindFirstValue(ClaimTypes.Name) ?? string.Empty);

            var propuestaRevisada = propuestas[indice];
            propuestas[indice] = propuestaRevisada with
            {
                RevisionesDocente = (propuestaRevisada.RevisionesDocente ?? [])
                    .Append(revision).ToArray()
            };
            ejecucion.ResultadosJson = JsonSerializer.Serialize(analisis with { Propuestas = propuestas }, JsonWeb);
            expediente.Estado = EstadoExpediente.EnRevision;
            await GuardarAsync(expediente, ct);

            return Ok(revision);
        }, ct);

    [HttpGet("analisis-ia")]
    public async Task<IActionResult> ConsultarAnalisis(Guid expedienteId, CancellationToken ct)
    {
        var expediente = await BuscarAsync(expedienteId, ct);
        if (expediente is null) return NotFound();
        var ejecucion = await db.EjecucionesReglas.AsNoTracking().SingleOrDefaultAsync(
            x => x.ExpedienteId == expedienteId && x.EsAnalisisIa, ct);
        if (ejecucion is null) return NoContent();
        var resultado = JsonSerializer.Deserialize<GeneradorPropuestas.Analisis>(ejecucion.ResultadosJson, JsonWeb)!;
        return Ok(new { ejecucion.Id,
            ejecutadoEn = DateTime.SpecifyKind(ejecucion.EjecutadoEn, DateTimeKind.Utc),
            estado = expediente.Estado.ToString(), resultado.Propuestas, resultado.Advertencias });
    }

    [HttpGet("decisiones")]
    public async Task<IActionResult> ConsultarDecisiones(Guid expedienteId, CancellationToken ct)
    {
        if (await BuscarAsync(expedienteId, ct) is null) return NotFound();
        return Ok(await DecisionesVigentesAsync(expedienteId, ct));
    }

    [HttpGet("auditoria")]
    public async Task<IActionResult> ConsultarAuditoria(Guid expedienteId, CancellationToken ct)
    {
        if (await BuscarAsync(expedienteId, ct) is null) return NotFound();
        return Ok(await db.EventosAuditoria.AsNoTracking().Where(x => x.ExpedienteId == expedienteId)
            .OrderBy(x => x.Fecha).ToListAsync(ct));
    }

    [HttpPost("en-revision")]
    [Authorize(Roles = AppRoles.Profesor)]
    public Task<IActionResult> IniciarRevision(Guid expedienteId, CancellationToken ct) =>
        EditarAsync(expedienteId, async (expediente, _) =>
        {
            if (expediente.Estado == EstadoExpediente.EnRevision) return Ok(new { estado = "EnRevision" });
            if (!await db.EjecucionesReglas.AnyAsync(x => x.ExpedienteId == expedienteId, ct))
                return Conflict(new { mensaje = "Evalúa el expediente antes de iniciar la revisión." });
            expediente.Estado = EstadoExpediente.EnRevision;
            await GuardarAsync(expediente, ct);
            return Ok(new { estado = "EnRevision" });
        }, ct);

    [HttpPost("resolver")]
    [Authorize(Roles = AppRoles.Profesor)]
    public Task<IActionResult> Resolver(Guid expedienteId, CancellationToken ct) =>
        EditarAsync(expedienteId, async (expediente, _) =>
        {
            if (expediente.Estado != EstadoExpediente.EnRevision)
                return Conflict(new { mensaje = "Solo se puede resolver un expediente en revisión." });
            var ejecucion = await db.EjecucionesReglas.AsNoTracking().SingleOrDefaultAsync(
                x => x.ExpedienteId == expedienteId && x.EsAnalisisIa, ct);
            if (ejecucion is null)
                return Conflict(new { mensaje = "Primero genera y revisa las propuestas." });
            var analisis = JsonSerializer.Deserialize<GeneradorPropuestas.Analisis>(ejecucion.ResultadosJson, JsonWeb)!;
            var destinos = analisis.Propuestas.Select(x => x.AsignaturaDestinoId).Distinct().ToHashSet();
            var decisiones = await DecisionesVigentesAsync(expedienteId, ct);
            if (destinos.Count == 0 || !destinos.SetEquals(decisiones.Select(x => x.AsignaturaDestinoId))
                || decisiones.Any(x => x.ResultadoFinal == 3))
                return Conflict(new { mensaje = "Registra una decisión concluyente por cada destino antes de resolver." });
            var origenes = decisiones.Where(x => x.ResultadoFinal is 0 or 4)
                .SelectMany(x => JsonSerializer.Deserialize<Guid[]>(x.OrigenIdsJson)!).ToArray();
            if (origenes.Distinct().Count() != origenes.Length)
                return Conflict(new { mensaje = "Una asignatura de origen no puede respaldar dos destinos aceptados." });
            expediente.Estado = EstadoExpediente.Resuelto;
            await GuardarAsync(expediente, ct);
            return Ok(new { estado = "Resuelto", soloLectura = true });
        }, ct);

    private async Task<List<Decision>> DecisionesVigentesAsync(Guid expedienteId, CancellationToken ct)
    {
        var decisiones = await db.Decisiones.AsNoTracking().Where(x => x.ExpedienteId == expedienteId)
            .OrderByDescending(x => x.RegistradoEn).ThenByDescending(x => x.Id).ToListAsync(ct);
        return decisiones.DistinctBy(x => x.AsignaturaDestinoId).ToList();
    }

    private async Task<IActionResult> EditarAsync(Guid id,
        Func<Expediente, AuthenticationHeaderValue, Task<IActionResult>> operacion, CancellationToken ct)
    {
        var expediente = await BuscarAsync(id, ct);
        if (expediente is null)
            return NotFound();

        if (expediente.Estado is not (EstadoExpediente.Borrador or EstadoExpediente.EnRevision or EstadoExpediente.AnalizadoConIa))
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
            return Conflict(new { mensaje = "El expediente cambió durante la operación. Vuelve a consultarlo." });
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
    public sealed record AgruparOrigenesDto(Guid AsignaturaDestinoId, IReadOnlyList<Guid> OrigenIds);
    public sealed record RevisionPropuestaIaDto(Guid AsignaturaDestinoId, IReadOnlyList<Guid> OrigenIds,
        bool AceptarPropuestaIa, int? ResultadoPropio, string? Observacion);
    private sealed record ResultadoPar(Guid ParId, Guid AsignaturaCursadaId, string NombreOrigen,
        Guid AsignaturaDestinoId, string NombreDestino, JsonElement Evaluacion);
}
